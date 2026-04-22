[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Token,
    [string]$RunnerDir = 'C:\actions-runner',
    [string]$RunnerName = 'eiti-lan-server',
    [string]$RepoUrl = 'https://github.com/eigies/eiti',
    [string]$Labels = 'self-hosted,windows,iis'
)

$ErrorActionPreference = 'Stop'

function Get-RunnerServiceName {
    param([string]$RunnerDir, [string]$RunnerName)

    $serviceFile = Join-Path $RunnerDir '.service'
    if (Test-Path $serviceFile) {
        $name = (Get-Content $serviceFile | Select-Object -First 1).Trim()
        if ($name) {
            return $name
        }
    }

    $service = Get-Service -Name "actions.runner.*.$RunnerName" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($service) {
        return $service.Name
    }

    return $null
}

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($id)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run as Administrator.'
}

if (-not (Test-Path $RunnerDir)) {
    New-Item -ItemType Directory -Path $RunnerDir -Force | Out-Null
}

$configPath = Join-Path $RunnerDir 'config.cmd'
$serviceName = Get-RunnerServiceName -RunnerDir $RunnerDir -RunnerName $RunnerName

if (Test-Path $configPath) {
    Write-Host 'Existing runner installation detected. Reusing current files...'
}
else {
    $existingEntries = Get-ChildItem -Path $RunnerDir -Force -ErrorAction SilentlyContinue
    if ($existingEntries) {
        throw "Runner directory '$RunnerDir' is not empty, but no existing runner install was detected."
    }

    Write-Host 'Fetching latest runner version...'
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/actions/runner/releases/latest' -Headers @{ 'User-Agent' = 'setup-runner' }
    $version = $release.tag_name.TrimStart('v')
    $zipName = "actions-runner-win-x64-$version.zip"
    $downloadUrl = "https://github.com/actions/runner/releases/download/v$version/$zipName"
    $zipPath = Join-Path $RunnerDir $zipName

    Write-Host "Downloading runner v$version..."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing

    Write-Host 'Extracting...'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $RunnerDir)
    Remove-Item $zipPath
}

Write-Host 'Configuring runner...'
Push-Location $RunnerDir
if ($serviceName -and (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) {
    Stop-Service -Name $serviceName -ErrorAction SilentlyContinue
}
.\config.cmd --url $RepoUrl --token $Token --name $RunnerName --labels $Labels --runasservice --unattended --replace
Pop-Location

# Change service logon to LocalSystem so deploy.ps1 can manage IIS
$serviceName = Get-RunnerServiceName -RunnerDir $RunnerDir -RunnerName $RunnerName
if (-not $serviceName) {
    throw "Runner service for '$RunnerName' was not found after configuration."
}

Write-Host "Configuring service '$serviceName' to run as LocalSystem..."
Stop-Service -Name $serviceName -ErrorAction SilentlyContinue
sc.exe config $serviceName obj= "LocalSystem" password= ""
Start-Service -Name $serviceName

Write-Host "`n=== Runner ready ===" -ForegroundColor Green
Write-Host "Verify at: https://github.com/eigies/eiti/settings/actions/runners"
