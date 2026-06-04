[CmdletBinding()]
param(
    [string]$HostHeader = '',
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
    [string]$SmokeHost = '127.0.0.1',
    [switch]$SkipSmokeTests,
    [switch]$SkipPrecheck
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) { Write-Host "`n=== $Message ===" -ForegroundColor Cyan }
function Require-Command([string]$Name) { if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) { throw "Missing command: $Name" } }
function Ensure-Directory([string]$Path) { if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Path $Path -Force | Out-Null } }
function Assert-LastExitCode([string]$Action) {
    if ($LASTEXITCODE -ne 0) {
        throw "$Action failed with exit code $LASTEXITCODE"
    }
}
function Ensure-DotNetEf([string]$ToolPath) {
    Ensure-Directory $ToolPath
    $exePath = Join-Path $ToolPath 'dotnet-ef.exe'
    if (Test-Path $exePath) {
        return $exePath
    }

    dotnet tool install dotnet-ef --tool-path $ToolPath | Out-Null
    Assert-LastExitCode -Action 'dotnet tool install dotnet-ef'
    if (-not (Test-Path $exePath)) {
        throw "dotnet-ef was installed but '$exePath' was not found."
    }

    return $exePath
}
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
    if ($id.User -and $id.User.IsWellKnown([Security.Principal.WellKnownSidType]::LocalSystemSid)) {
        return
    }
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
'        <rule name="ApiSubAppCompat" stopProcessing="true">',
'          <match url="^api($|/.*)" />',
'          <action type="Rewrite" url="/api/{R:0}" />',
'        </rule>',
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

function Configure-IIS([string]$Name, [string]$BindingHostHeader, [int]$SitePort, [string]$FrontPath, [string]$ApiPath) {
    $frontPool = "$Name-FrontPool"
    $apiPool = "$Name-ApiPool"

    if (-not (Test-Path "IIS:\AppPools\$frontPool")) { New-WebAppPool -Name $frontPool | Out-Null }
    Set-ItemProperty "IIS:\AppPools\$frontPool" managedRuntimeVersion ''

    if (-not (Test-Path "IIS:\AppPools\$apiPool")) { New-WebAppPool -Name $apiPool | Out-Null }
    Set-ItemProperty "IIS:\AppPools\$apiPool" managedRuntimeVersion ''

    if (-not (Get-Website -Name $Name -ErrorAction SilentlyContinue)) {
        if ([string]::IsNullOrWhiteSpace($BindingHostHeader)) {
            New-Website -Name $Name -PhysicalPath $FrontPath -Port $SitePort -ApplicationPool $frontPool | Out-Null
        } else {
            New-Website -Name $Name -PhysicalPath $FrontPath -Port $SitePort -HostHeader $BindingHostHeader -ApplicationPool $frontPool | Out-Null
        }
    } else {
        Set-ItemProperty "IIS:\Sites\$Name" -Name physicalPath -Value $FrontPath
        Set-ItemProperty "IIS:\Sites\$Name" -Name applicationPool -Value $frontPool
        Get-WebBinding -Name $Name -Protocol 'http' | ForEach-Object {
            Remove-WebBinding -Name $Name -BindingInformation $_.bindingInformation -Protocol 'http'
        }
        if ([string]::IsNullOrWhiteSpace($BindingHostHeader)) {
            New-WebBinding -Name $Name -Protocol http -Port $SitePort | Out-Null
        } else {
            New-WebBinding -Name $Name -Protocol http -Port $SitePort -HostHeader $BindingHostHeader | Out-Null
        }
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

function Run-Smoke([string]$RequestHost, [int]$SitePort) {
    $base = if ($SitePort -eq 80) { "http://$RequestHost" } else { "http://$RequestHost`:$SitePort" }
    $front = Invoke-WebRequest -Uri "$base/" -UseBasicParsing
    if ($front.StatusCode -ge 400) { throw "Front check failed: $($front.StatusCode)" }

    $apiCandidates = @(
        "$base/api/users/me",
        "$base/api/api/users/me"
    )

    foreach ($candidate in $apiCandidates) {
        try {
            Invoke-WebRequest -Uri $candidate -UseBasicParsing -ErrorAction Stop | Out-Null
            return
        } catch {
            if ($_.Exception.Response -eq $null) { throw }
            $status = [int]$_.Exception.Response.StatusCode
            if ($status -eq 401 -or $status -eq 403) {
                return
            }

            if ($status -ne 404) {
                throw "API check unexpected status at $candidate`: $status"
            }
        }
    }

    throw "API check failed. None of these endpoints responded with an expected status: $($apiCandidates -join ', ')"
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
$npmExit = $LASTEXITCODE
npm run build
$buildExit = $LASTEXITCODE
Pop-Location
$LASTEXITCODE = $npmExit
Assert-LastExitCode -Action 'npm ci'
$LASTEXITCODE = $buildExit
Assert-LastExitCode -Action 'npm run build'

$distA = Join-Path $FrontRootPath 'dist\eiti-front\browser'
$distB = Join-Path $FrontRootPath 'dist\eiti-front'
$dist = if (Test-Path $distA) { $distA } elseif (Test-Path $distB) { $distB } else { throw 'dist path not found' }
Copy-CleanContent -Source $dist -Destination $FrontPublishPath
Write-FrontWebConfig -Path $FrontPublishPath

Write-Step 'Publish API'
$apiPool = "$SiteName-ApiPool"

# Publicar a una carpeta de staging: nunca escribir sobre el directorio vivo mientras IIS mantiene
# lockeadas las DLL (eso hacia que el publish "terminara OK" sin reemplazar binarios). El swap real
# se hace mas abajo (paso 'Swap API binaries'), despues de migraciones y parando el app pool.
$ApiStagePath = "${ApiPublishPath}_stage"
if (Test-Path $ApiStagePath) { Remove-Item $ApiStagePath -Recurse -Force }
Ensure-Directory $ApiStagePath
dotnet publish $ApiProjectPath -c Release -o $ApiStagePath
Assert-LastExitCode -Action 'dotnet publish'

if (-not $SkipMigrations) {
    Write-Step 'Run EF migrations'
    $env:ConnectionStrings__DefaultConnection = $ConnectionString
    $infraProjectPath = Resolve-FirstExistingPath -Candidates @(
        (Join-Path $backendRoot 'eiti.Infrastructure\eiti.Infrastructure.csproj'),
        'C:\Eiti\eiti\eiti.Infrastructure\eiti.Infrastructure.csproj',
        'C:\eiti\eiti.Infrastructure\eiti.Infrastructure.csproj'
    ) -Label 'Infrastructure project path'
    $dotnetEfPath = Ensure-DotNetEf -ToolPath 'C:\eiti\tools\dotnet-ef'
    & $dotnetEfPath database update --project $infraProjectPath --startup-project $ApiProjectPath --context ApplicationDbContext --configuration Release
    Assert-LastExitCode -Action 'dotnet ef database update'
    Remove-Item Env:\ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
}

Write-Step 'Swap API binaries'
# Parar el app pool del API para que el worker libere los handles de las DLL antes del swap.
# (Las migraciones ya corrieron con la app vieja sirviendo; recien ahora bajamos el API.)
Import-Module WebAdministration -ErrorAction SilentlyContinue
if (Test-Path "IIS:\AppPools\$apiPool") {
    Write-Host "Stopping app pool '$apiPool' to release DLL locks..."
    Stop-WebAppPool -Name $apiPool -ErrorAction SilentlyContinue
    $deadline = (Get-Date).AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 500
        $poolState = (Get-WebAppPoolState -Name $apiPool -ErrorAction SilentlyContinue).Value
    } while ($poolState -ne 'Stopped' -and (Get-Date) -lt $deadline)
}

# Respaldo: app_offline.htm hace que el ASP.NET Core Module apague la app in-process y suelte los
# handles aunque el worker siguiera vivo (por si el stop del pool tuviera una carrera).
Ensure-Directory $ApiPublishPath
Set-Content -Path (Join-Path $ApiPublishPath 'app_offline.htm') -Value '<html><body>Actualizando...</body></html>' -Encoding utf8
Start-Sleep -Seconds 1

# Reemplazar binarios (con reintentos por si algun handle tarda en soltarse). Copy-CleanContent limpia
# el destino, asi que el app_offline.htm se elimina aca y la app vuelve online al iniciar el pool.
$swapAttempts = 0
while ($true) {
    try {
        Copy-CleanContent -Source $ApiStagePath -Destination $ApiPublishPath
        break
    } catch {
        $swapAttempts++
        if ($swapAttempts -ge 5) { throw "No se pudieron reemplazar los binarios del API (locked): $($_.Exception.Message)" }
        Write-Host "Swap retry $swapAttempts (archivos lockeados)..."
        Start-Sleep -Seconds 2
    }
}
Remove-Item $ApiStagePath -Recurse -Force -ErrorAction SilentlyContinue
Set-ApiAppSettings -ApiPath $ApiPublishPath -ConnString $ConnectionString

Write-Step 'Configure IIS'
Configure-IIS -Name $SiteName -BindingHostHeader $HostHeader -SitePort $Port -FrontPath $FrontPublishPath -ApiPath $ApiPublishPath
Ensure-FirewallRule -RulePort $Port

if (-not $SkipSmokeTests) {
    Write-Step 'Smoke tests'
    Run-Smoke -RequestHost $SmokeHost -SitePort $Port
}

$displayHost = if ([string]::IsNullOrWhiteSpace($HostHeader)) { '<server-ip>' } else { $HostHeader }
$url = if ($Port -eq 80) { "http://$displayHost/" } else { "http://$displayHost`:$Port/" }
Write-Step 'Done'
Write-Host "Front URL: $url"
Write-Host "API URL: $($url.TrimEnd('/'))/api"
