[CmdletBinding()]
param(
    [string]$LocalUrl = 'http://127.0.0.1:80',
    [string]$ToolRoot = 'C:\eiti\tools\ngrok',
    [string]$NgrokExe = 'C:\Users\eitip\AppData\Local\Microsoft\WinGet\Packages\Ngrok.Ngrok_Microsoft.Winget.Source_8wekyb3d8bbwe\ngrok.exe',
    [string]$TaskName = 'EitiNgrokTunnel',
    [Parameter(Mandatory)]
    [string]$Authtoken
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) { Write-Host "`n=== $Message ===" -ForegroundColor Cyan }
function Write-Ok([string]$Message) { Write-Host "[OK] $Message" -ForegroundColor Green }
function Remove-NgrokTask([string]$Name) {
    $task = Get-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue
    if (-not $task) {
        return
    }

    try {
        Stop-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue
    }
    catch {
    }

    Unregister-ScheduledTask -TaskName $Name -Confirm:$false
}

if (-not (Test-Path $NgrokExe)) {
    throw "ngrok executable not found at '$NgrokExe'."
}

Write-Step 'Prepare ngrok'
New-Item -ItemType Directory -Path $ToolRoot -Force | Out-Null

$localUri = [Uri]$LocalUrl
$addr = if ($localUri.Port -gt 0) {
    "$($localUri.Host):$($localUri.Port)"
} else {
    $localUri.Host
}

$configPath = Join-Path $ToolRoot 'ngrok-ci.yml'
$stdoutLog = Join-Path $ToolRoot 'ngrok.log'
$stderrLog = Join-Path $ToolRoot 'ngrok.err.log'
$runnerScript = Join-Path $PSScriptRoot 'run-ngrok-task.ps1'

@"
version: "2"
authtoken: $Authtoken
tunnels:
  eiti:
    proto: http
    addr: $addr
"@ | Set-Content -Path $configPath -NoNewline

Get-Process ngrok -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item $stdoutLog, $stderrLog -ErrorAction SilentlyContinue
Remove-NgrokTask -Name $TaskName

Write-Step 'Start ngrok tunnel'
$actionArgs = @(
    '-ExecutionPolicy', 'Bypass',
    '-File', $runnerScript,
    '-NgrokExe', $NgrokExe,
    '-ConfigPath', $configPath,
    '-StdoutLog', $stdoutLog,
    '-StderrLog', $stderrLog
)
$quotedActionArgs = $actionArgs | ForEach-Object {
    if ($_ -match '\s') { '"{0}"' -f $_ } else { $_ }
}
$action = New-ScheduledTaskAction -Execute 'PowerShell.exe' -Argument ($quotedActionArgs -join ' ')
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
Register-ScheduledTask -TaskName $TaskName -Action $action -Principal $principal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName $TaskName

$publicUrl = $null
for ($i = 0; $i -lt 45; $i++) {
    Start-Sleep -Seconds 2

    try {
        $response = Invoke-RestMethod -Uri 'http://127.0.0.1:4040/api/tunnels'
        $publicUrl = $response.tunnels |
            Where-Object { $_.public_url -like 'https://*' } |
            Select-Object -First 1 -ExpandProperty public_url
    }
    catch {
        $publicUrl = $null
    }

    if ($publicUrl) {
        break
    }
}

if (-not $publicUrl) {
    throw 'Could not retrieve the public ngrok URL.'
}

Write-Step 'Tunnel ready'
Write-Host "Public URL: $publicUrl"
Write-Host "Task name: $TaskName"
Write-Host "Stdout log: $stdoutLog"
Write-Host "Stderr log: $stderrLog"
Write-Ok 'ngrok tunnel is running.'
Write-Output $publicUrl
