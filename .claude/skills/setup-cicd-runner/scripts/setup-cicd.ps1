[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ConnectionString,
    [string]$GhToken
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) { Write-Host "`n=== $Message ===" -ForegroundColor Cyan }
function Write-Ok([string]$Message)   { Write-Host "[OK] $Message" -ForegroundColor Green }
function Get-GhCliPath {
    $command = Get-Command 'gh' -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        'C:\Program Files\GitHub CLI\gh.exe',
        (Join-Path $env:LOCALAPPDATA 'Programs\GitHub CLI\gh.exe')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($id)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run as Administrator.'
}

# --- Step 1: GitHub CLI ---
Write-Step 'Checking GitHub CLI'
$ghPath = Get-GhCliPath
if (-not $ghPath) {
    Write-Host 'gh CLI not found. Installing via winget...'
    winget install --id GitHub.cli --exact --silent --accept-source-agreements --accept-package-agreements
    $env:PATH = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' +
                [System.Environment]::GetEnvironmentVariable('PATH', 'User') + ';' +
                $env:PATH
    $ghPath = Get-GhCliPath
}
if (-not $ghPath) {
    throw 'gh CLI installed but executable not found.'
}
Write-Ok "gh CLI available at $ghPath"

# --- Step 2: Frontend repo ---
Write-Step 'Checking frontend repository'
$frontRoot = 'C:\eiti-front'
if (-not (Test-Path (Join-Path $frontRoot '.git'))) {
    throw 'Frontend repo not found at C:\eiti-front.'
}
git -C $frontRoot rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'Frontend path exists but is not a valid git repository.'
}
Write-Ok 'Frontend repo detected'

# --- Step 3: Auth ---
Write-Step 'Authenticating with GitHub'
if ($GhToken) {
    $env:GH_TOKEN = $GhToken
    Write-Ok 'Using provided token'
} else {
    $authStatus = Start-Process -FilePath $ghPath -ArgumentList @('auth', 'status', '--hostname', 'github.com') -Wait -PassThru -NoNewWindow
    if ($authStatus.ExitCode -ne 0) {
        throw 'Not authenticated. Provide -GhToken <PAT> or run: gh auth login'
    }
    Write-Ok 'Already authenticated'
}

# --- Step 4: Set deploy secret ---
Write-Step 'Setting GitHub secret DEPLOY_CONNECTION_STRING'
& $ghPath secret set DEPLOY_CONNECTION_STRING --body $ConnectionString --repo eigies/eiti
if ($LASTEXITCODE -ne 0) { throw 'Failed to set secret' }
Write-Ok 'Secret set'

# --- Step 5: Runner registration token ---
Write-Step 'Getting runner registration token'
$response = (& $ghPath api --method POST /repos/eigies/eiti/actions/runners/registration-token) | ConvertFrom-Json
if (-not $response.token) { throw 'Empty token in response' }
Write-Ok 'Token obtained'

# --- Step 6: Install runner ---
Write-Step 'Installing GitHub Actions runner'
$setupRunner = Join-Path $PSScriptRoot '..\..\deploy-lan-iis\scripts\setup-runner.ps1'
& $setupRunner -Token $response.token

Write-Host "`n=== CI/CD setup complete ===" -ForegroundColor Green
Write-Host 'Push to main to trigger the first deploy.'
Write-Host 'Monitor deploys at: https://github.com/eigies/eiti/actions'
