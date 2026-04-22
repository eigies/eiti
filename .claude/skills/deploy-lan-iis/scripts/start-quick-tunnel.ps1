[CmdletBinding()]
param(
    [string]$LocalUrl = 'http://127.0.0.1:80',
    [string]$ToolRoot = 'C:\eiti\tools\cloudflared'
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) { Write-Host "`n=== $Message ===" -ForegroundColor Cyan }
function Write-Ok([string]$Message) { Write-Host "[OK] $Message" -ForegroundColor Green }

$exePath = Join-Path $ToolRoot 'cloudflared.exe'
$stdoutLog = Join-Path $ToolRoot 'tunnel.log'
$stderrLog = Join-Path $ToolRoot 'tunnel.err.log'

Write-Step 'Prepare cloudflared'
New-Item -ItemType Directory -Path $ToolRoot -Force | Out-Null

if (-not (Test-Path $exePath)) {
    Invoke-WebRequest `
        -Uri 'https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe' `
        -OutFile $exePath
}

Get-Process cloudflared -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item $stdoutLog, $stderrLog -ErrorAction SilentlyContinue

Write-Step 'Start quick tunnel'
Start-Process `
    -FilePath $exePath `
    -ArgumentList "tunnel --url $LocalUrl --no-autoupdate" `
    -RedirectStandardOutput $stdoutLog `
    -RedirectStandardError $stderrLog `
    -WindowStyle Hidden

$publicUrl = $null
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Seconds 1

    foreach ($logPath in @($stdoutLog, $stderrLog)) {
        if (-not (Test-Path $logPath)) {
            continue
        }

        $match = Select-String -Path $logPath -Pattern 'https://[-a-z0-9]+\.trycloudflare\.com' -AllMatches
        if ($match) {
            $publicUrl = $match.Matches[-1].Value
            break
        }
    }

    if ($publicUrl) {
        break
    }
}

if (-not $publicUrl) {
    throw 'Could not retrieve the public Cloudflare tunnel URL.'
}

Write-Step 'Tunnel ready'
Write-Host "Public URL: $publicUrl"
Write-Host "Stdout log: $stdoutLog"
Write-Host "Stderr log: $stderrLog"
Write-Ok 'Quick tunnel is running.'
