[CmdletBinding()]
param(
    [switch]$AutoInstall = $true,
    [switch]$SkipIIS,
    [switch]$SkipHostingBundle,
    [switch]$SkipSqlCmd,
    [switch]$SkipUrlRewrite
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) { Write-Host "`n=== $Message ===" -ForegroundColor Cyan }
function Write-Ok([string]$Message) { Write-Host "[OK] $Message" -ForegroundColor Green }
function Write-Warn([string]$Message) { Write-Host "[WARN] $Message" -ForegroundColor Yellow }

function Ensure-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script in elevated PowerShell (Administrator).'
    }
}

function Has-Command([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Has-Winget {
    return Has-Command 'winget'
}

function Install-WithWinget {
    param(
        [string[]]$Ids,
        [string]$Label
    )

    if (-not (Has-Winget)) {
        throw "winget is required to auto-install $Label."
    }

    foreach ($id in $Ids) {
        Write-Host "Trying winget package: $id"
        $args = @('install','--id',$id,'--exact','--silent','--accept-source-agreements','--accept-package-agreements')
        $proc = Start-Process -FilePath 'winget' -ArgumentList $args -PassThru -Wait -NoNewWindow
        if ($proc.ExitCode -eq 0) {
            Write-Ok "$Label installed with $id"
            return
        }
    }

    throw "Could not install $Label with winget. Tried: $($Ids -join ', ')"
}

function Ensure-CommandInstalled {
    param(
        [string]$CommandName,
        [string]$Label,
        [string[]]$WingetIds
    )

    if (Has-Command $CommandName) {
        Write-Ok "$Label detected"
        return
    }

    if (-not $AutoInstall) {
        throw "$Label is missing"
    }

    Write-Warn "$Label not found. Installing..."
    Install-WithWinget -Ids $WingetIds -Label $Label

    if (-not (Has-Command $CommandName)) {
        throw "$Label install finished but command '$CommandName' is still missing. Open a new terminal and rerun."
    }

    Write-Ok "$Label ready"
}

function Ensure-DotNetEf {
    $toolList = dotnet tool list -g | Out-String
    if ($toolList -match 'dotnet-ef') {
        Write-Ok 'dotnet-ef global tool detected'
        return
    }

    if (-not $AutoInstall) {
        throw 'dotnet-ef tool is missing'
    }

    Write-Warn 'dotnet-ef not found. Installing global tool...'
    dotnet tool install --global dotnet-ef
    $env:PATH = "$env:PATH;$env:USERPROFILE\.dotnet\tools"

    $toolList = dotnet tool list -g | Out-String
    if ($toolList -notmatch 'dotnet-ef') {
        throw 'dotnet-ef installation failed'
    }

    Write-Ok 'dotnet-ef installed'
}

function Ensure-IISFeature {
    param([string[]]$FeatureNames)

    foreach ($feature in $FeatureNames) {
        $state = (Get-WindowsOptionalFeature -Online -FeatureName $feature -ErrorAction Stop).State
        if ($state -eq 'Enabled') {
            Write-Ok "$feature enabled"
            continue
        }

        if (-not $AutoInstall) {
            throw "Windows feature $feature is disabled"
        }

        Write-Warn "Enabling $feature..."
        Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart | Out-Null
        Write-Ok "$feature enabled"
    }
}

function Ensure-IIS {
    if ($SkipIIS) {
        Write-Warn 'Skipping IIS checks by parameter'
        return
    }

    Write-Step 'Checking IIS features'
    Ensure-IISFeature -FeatureNames @(
        'IIS-WebServerRole',
        'IIS-WebServer',
        'IIS-CommonHttpFeatures',
        'IIS-StaticContent',
        'IIS-DefaultDocument',
        'IIS-HttpErrors',
        'IIS-ApplicationDevelopment',
        'IIS-ISAPIExtensions',
        'IIS-ISAPIFilter',
        'IIS-ManagementConsole',
        'IIS-ManagementScriptingTools'
    )

    if (-not (Test-Path 'C:\Windows\System32\inetsrv\appcmd.exe')) {
        throw 'IIS appcmd.exe not found after enabling features.'
    }

    Write-Ok 'IIS detected'
}

function Ensure-HostingBundle {
    if ($SkipHostingBundle) {
        Write-Warn 'Skipping ASP.NET Core Hosting Bundle check by parameter'
        return
    }

    Write-Step 'Checking ASP.NET Core Hosting Bundle'
    $modulePath = 'C:\Program Files\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'
    if (Test-Path $modulePath) {
        Write-Ok 'ASP.NET Core Hosting Bundle detected'
        return
    }

    if (-not $AutoInstall) {
        throw 'ASP.NET Core Hosting Bundle is missing'
    }

    Write-Warn 'Hosting Bundle missing. Installing...'
    Install-WithWinget -Ids @(
        'Microsoft.DotNet.HostingBundle.10',
        'Microsoft.DotNet.HostingBundle.9',
        'Microsoft.DotNet.HostingBundle.8'
    ) -Label 'ASP.NET Core Hosting Bundle'

    if (-not (Test-Path $modulePath)) {
        throw 'Hosting Bundle install finished but AspNetCoreModuleV2 is still missing. A reboot may be required.'
    }

    Write-Ok 'ASP.NET Core Hosting Bundle ready'
}

function Ensure-UrlRewrite {
    if ($SkipUrlRewrite) {
        Write-Warn 'Skipping URL Rewrite check by parameter'
        return
    }

    Write-Step 'Checking IIS URL Rewrite module'
    $paths = @(
        'C:\Program Files\IIS\URL Rewrite\rewrite.dll',
        'HKLM:\SOFTWARE\Microsoft\IIS Extensions\URL Rewrite'
    )

    foreach ($path in $paths) {
        if (Test-Path $path) {
            Write-Ok 'IIS URL Rewrite detected'
            return
        }
    }

    if (-not $AutoInstall) {
        throw 'IIS URL Rewrite module is missing'
    }

    Write-Warn 'URL Rewrite missing. Trying winget package...'
    try {
        Install-WithWinget -Ids @('IIS.URLRewrite') -Label 'IIS URL Rewrite'
    }
    catch {
        Write-Warn 'Could not auto-install URL Rewrite. Install it manually from Microsoft.'
        return
    }

    foreach ($path in $paths) {
        if (Test-Path $path) {
            Write-Ok 'IIS URL Rewrite ready'
            return
        }
    }

    Write-Warn 'URL Rewrite still not detected. Manual install may be required.'
}

function Ensure-SqlCmd {
    if ($SkipSqlCmd) {
        Write-Warn 'Skipping sqlcmd check by parameter'
        return
    }

    Write-Step 'Checking sqlcmd'
    if (Has-Command 'sqlcmd') {
        Write-Ok 'sqlcmd detected'
        return
    }

    if (-not $AutoInstall) {
        throw 'sqlcmd is missing'
    }

    Write-Warn 'sqlcmd missing. Installing...'
    Install-WithWinget -Ids @('Microsoft.Sqlcmd') -Label 'sqlcmd'

    if (-not (Has-Command 'sqlcmd')) {
        throw 'sqlcmd installation failed'
    }

    Write-Ok 'sqlcmd ready'
}

Write-Step 'Precheck start'
Ensure-Admin

Write-Step 'Checking core toolchain'
Ensure-CommandInstalled -CommandName 'dotnet' -Label '.NET SDK' -WingetIds @('Microsoft.DotNet.SDK.10','Microsoft.DotNet.SDK.9','Microsoft.DotNet.SDK.8')
Ensure-CommandInstalled -CommandName 'node' -Label 'Node.js LTS' -WingetIds @('OpenJS.NodeJS.LTS')
Ensure-CommandInstalled -CommandName 'npm' -Label 'npm' -WingetIds @('OpenJS.NodeJS.LTS')
Ensure-DotNetEf

Ensure-IIS
Ensure-HostingBundle
Ensure-UrlRewrite
Ensure-SqlCmd

Write-Step 'Precheck complete'
Write-Ok 'Machine is ready (or warnings above indicate manual steps).'
