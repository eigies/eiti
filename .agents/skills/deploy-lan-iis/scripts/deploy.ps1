[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$HostHeader,
    [int]$Port = 80,
    [string]$SiteName = 'EitiSite',
    [string]$ApiProjectPath,
    [string]$ApiPublishPath = 'C:\inetpub\eiti\api',
    [string]$FrontRootPath,
    [string]$FrontPublishPath = 'C:\inetpub\eiti\front',
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,
    [string]$EnvironmentName = 'Production',
    [switch]$SkipMigrations,
    [switch]$SkipSmokeTests,
    [switch]$SkipPrecheck
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) { Write-Host "`n=== $Message ===" -ForegroundColor Cyan }
function Require-Command([string]$Name) { if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) { throw "Missing command: $Name" } }
function Ensure-Directory([string]$Path) { if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Path $Path -Force | Out-Null } }
function Resolve-FirstExistingPath([string[]]$Candidates, [string]$Label) {
    foreach ($candidate in $Candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if (Test-Path $candidate) { return (Resolve-Path $candidate).Path }
    }

    throw "$Label not found. Checked: $($Candidates -join ', ')"
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$precheckPath = Join-Path $scriptDir 'precheck.ps1'
$backendRoot = Resolve-Path (Join-Path $scriptDir '..\..\..\..')

if ([string]::IsNullOrWhiteSpace($ApiProjectPath)) {
    $ApiProjectPath = Resolve-FirstExistingPath -Candidates @(
        (Join-Path $backendRoot 'eiti.Api\eiti.Api.csproj'),
        'C:\Eiti\eiti\eiti.Api\eiti.Api.csproj',
        'C:\eiti\eiti.Api\eiti.Api.csproj'
    ) -Label 'API project path'
}

if ([string]::IsNullOrWhiteSpace($FrontRootPath)) {
    $FrontRootPath = Resolve-FirstExistingPath -Candidates @(
        $env:EITI_FRONT_ROOT,
        (Join-Path (Split-Path $backendRoot -Parent) 'eiti-front'),
        'C:\EiTeFront\eiti-front',
        'C:\eiti-front'
    ) -Label 'Frontend root path'
}

if (-not $SkipPrecheck) {
    if (-not (Test-Path $precheckPath)) {
        throw "precheck.ps1 not found at $precheckPath"
    }

    Write-Step 'Running precheck'
    & $precheckPath -AutoInstall
}

function Ensure-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run as Administrator.'
    }
}

function Ensure-IIS {
    if (-not (Test-Path 'C:\Windows\System32\inetsrv\appcmd.exe')) {
        throw 'IIS not detected. Install IIS + ASP.NET Core Hosting Bundle first.'
    }
    Import-Module WebAdministration -ErrorAction Stop
}

function Copy-CleanContent([string]$Source, [string]$Destination) {
    Ensure-Directory $Destination
    if (Test-Path (Join-Path $Destination '*')) { Remove-Item (Join-Path $Destination '*') -Recurse -Force }
    Copy-Item (Join-Path $Source '*') $Destination -Recurse -Force
}

function Write-FrontWebConfig([string]$Path) {
    $lines = @(
'<?xml version="1.0" encoding="utf-8"?>',
'<configuration>',
'  <system.webServer>',
'    <rewrite>',
'      <rules>',
'        <rule name="AngularRoutes" stopProcessing="true">',
'          <match url=".*" />',
'          <conditions logicalGrouping="MatchAll">',
'            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />',
'            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />',
'            <add input="{REQUEST_URI}" pattern="^/api" negate="true" />',
'          </conditions>',
'          <action type="Rewrite" url="/index.html" />',
'        </rule>',
'      </rules>',
'    </rewrite>',
'  </system.webServer>',
'</configuration>'
    )
    Set-Content -Path (Join-Path $Path 'web.config') -Value $lines -Encoding utf8
}

function Set-ApiAppSettings([string]$ApiPath, [string]$ConnString) {
    $payload = @{
        ConnectionStrings = @{ DefaultConnection = $ConnString }
        AllowedHosts = '*'
    } | ConvertTo-Json -Depth 5
    Set-Content -Path (Join-Path $ApiPath 'appsettings.Production.json') -Value $payload -Encoding utf8
}

function Configure-IIS([string]$Name, [string]$Host, [int]$SitePort, [string]$FrontPath, [string]$ApiPath) {
    $frontPool = "$Name-FrontPool"
    $apiPool = "$Name-ApiPool"

    if (-not (Test-Path "IIS:\AppPools\$frontPool")) { New-WebAppPool -Name $frontPool | Out-Null }
    Set-ItemProperty "IIS:\AppPools\$frontPool" managedRuntimeVersion ''

    if (-not (Test-Path "IIS:\AppPools\$apiPool")) { New-WebAppPool -Name $apiPool | Out-Null }
    Set-ItemProperty "IIS:\AppPools\$apiPool" managedRuntimeVersion ''

    if (-not (Get-Website -Name $Name -ErrorAction SilentlyContinue)) {
        New-Website -Name $Name -PhysicalPath $FrontPath -Port $SitePort -HostHeader $Host -ApplicationPool $frontPool | Out-Null
    } else {
        Set-ItemProperty "IIS:\Sites\$Name" -Name physicalPath -Value $FrontPath
        Set-ItemProperty "IIS:\Sites\$Name" -Name applicationPool -Value $frontPool
        Get-WebBinding -Name $Name -Protocol 'http' | ForEach-Object {
            Remove-WebBinding -Name $Name -BindingInformation $_.bindingInformation -Protocol 'http'
        }
        New-WebBinding -Name $Name -Protocol http -Port $SitePort -HostHeader $Host | Out-Null
    }

    if (Get-WebApplication -Site $Name -Name 'api' -ErrorAction SilentlyContinue) {
        Remove-WebApplication -Site $Name -Name 'api'
    }
    New-WebApplication -Site $Name -Name 'api' -PhysicalPath $ApiPath -ApplicationPool $apiPool | Out-Null

    Start-WebAppPool -Name $frontPool | Out-Null
    Start-WebAppPool -Name $apiPool | Out-Null
    Start-Website -Name $Name | Out-Null
}

function Ensure-FirewallRule([int]$RulePort) {
    $ruleName = "Eiti LAN HTTP $RulePort"
    if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $RulePort | Out-Null
    }
}

function Run-Smoke([string]$Host, [int]$SitePort) {
    $base = if ($SitePort -eq 80) { "http://$Host" } else { "http://$Host`:$SitePort" }
    $front = Invoke-WebRequest -Uri "$base/" -UseBasicParsing
    if ($front.StatusCode -ge 400) { throw "Front check failed: $($front.StatusCode)" }
    try {
        Invoke-WebRequest -Uri "$base/api/users/me" -UseBasicParsing -ErrorAction Stop | Out-Null
    } catch {
        if ($_.Exception.Response -eq $null) { throw }
        $status = [int]$_.Exception.Response.StatusCode
        if ($status -ne 401 -and $status -ne 403) { throw "API check unexpected status: $status" }
    }
}

Write-Step 'Validate prerequisites'
Ensure-Admin
Require-Command 'dotnet'
Require-Command 'node'
Require-Command 'npm'
Ensure-IIS

Write-Step 'Build frontend'
Push-Location $FrontRootPath
npm ci
npm run build
Pop-Location

$distA = Join-Path $FrontRootPath 'dist\eiti-front\browser'
$distB = Join-Path $FrontRootPath 'dist\eiti-front'
$dist = if (Test-Path $distA) { $distA } elseif (Test-Path $distB) { $distB } else { throw 'dist path not found' }
Copy-CleanContent -Source $dist -Destination $FrontPublishPath
Write-FrontWebConfig -Path $FrontPublishPath

Write-Step 'Publish API'
Ensure-Directory $ApiPublishPath
dotnet publish $ApiProjectPath -c Release -o $ApiPublishPath
Set-ApiAppSettings -ApiPath $ApiPublishPath -ConnString $ConnectionString

if (-not $SkipMigrations) {
    Write-Step 'Run EF migrations'
    $env:ConnectionStrings__DefaultConnection = $ConnectionString
    $infraProjectPath = Resolve-FirstExistingPath -Candidates @(
        (Join-Path $backendRoot 'eiti.Infrastructure\eiti.Infrastructure.csproj'),
        'C:\Eiti\eiti\eiti.Infrastructure\eiti.Infrastructure.csproj',
        'C:\eiti\eiti.Infrastructure\eiti.Infrastructure.csproj'
    ) -Label 'Infrastructure project path'
    dotnet ef database update --project $infraProjectPath --startup-project $ApiProjectPath --context ApplicationDbContext --configuration Release
    Remove-Item Env:\ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
}

Write-Step 'Configure IIS'
Configure-IIS -Name $SiteName -Host $HostHeader -SitePort $Port -FrontPath $FrontPublishPath -ApiPath $ApiPublishPath
Ensure-FirewallRule -RulePort $Port

if (-not $SkipSmokeTests) {
    Write-Step 'Smoke tests'
    Run-Smoke -Host $HostHeader -SitePort $Port
}

$url = if ($Port -eq 80) { "http://$HostHeader/" } else { "http://$HostHeader`:$Port/" }
Write-Step 'Done'
Write-Host "Front URL: $url"
Write-Host "API URL: $($url.TrimEnd('/'))/api"
