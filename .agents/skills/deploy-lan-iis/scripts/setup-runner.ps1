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

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($id)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run as Administrator.'
}

if (-not (Test-Path $RunnerDir)) {
    New-Item -ItemType Directory -Path $RunnerDir -Force | Out-Null
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

Write-Host 'Configuring runner...'
Push-Location $RunnerDir
.\config.cmd --url $RepoUrl --token $Token --name $RunnerName --labels $Labels --runasservice --unattended --replace
Pop-Location

# Change service logon to LocalSystem so deploy.ps1 can manage IIS
$serviceName = "actions.runner.eigies.eiti.$RunnerName"
Write-Host "Configuring service '$serviceName' to run as LocalSystem..."
Stop-Service -Name $serviceName -ErrorAction SilentlyContinue
sc.exe config $serviceName obj= "LocalSystem" password= ""
Start-Service -Name $serviceName

Write-Host "`n=== Runner ready ===" -ForegroundColor Green
Write-Host "Verify at: https://github.com/eigies/eiti/settings/actions/runners"
