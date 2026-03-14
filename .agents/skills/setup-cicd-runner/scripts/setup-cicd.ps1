[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ConnectionString,
    [string]$GhToken
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) { Write-Host "`n=== $Message ===" -ForegroundColor Cyan }
function Write-Ok([string]$Message)   { Write-Host "[OK] $Message" -ForegroundColor Green }

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($id)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run as Administrator.'
}

# --- Step 1: GitHub CLI ---
Write-Step 'Checking GitHub CLI'
if (-not (Get-Command 'gh' -ErrorAction SilentlyContinue)) {
    Write-Host 'gh CLI not found. Installing via winget...'
    winget install --id GitHub.cli --exact --silent --accept-source-agreements --accept-package-agreements
    $env:PATH = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' +
                [System.Environment]::GetEnvironmentVariable('PATH', 'User')
    if (-not (Get-Command 'gh' -ErrorAction SilentlyContinue)) {
        throw 'gh CLI installed but command not found. Open a new terminal and rerun.'
    }
}
Write-Ok 'gh CLI available'

# --- Step 2: Auth ---
Write-Step 'Authenticating with GitHub'
if ($GhToken) {
    $env:GH_TOKEN = $GhToken
    Write-Ok 'Using provided token'
} else {
    gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Not authenticated. Provide -GhToken <PAT> or run: gh auth login'
    }
    Write-Ok 'Already authenticated'
}

# --- Step 3: Set deploy secret ---
Write-Step 'Setting GitHub secret DEPLOY_CONNECTION_STRING'
gh secret set DEPLOY_CONNECTION_STRING --body $ConnectionString --repo eigies/eiti
if ($LASTEXITCODE -ne 0) { throw 'Failed to set secret' }
Write-Ok 'Secret set'

# --- Step 4: Runner registration token ---
Write-Step 'Getting runner registration token'
$response = gh api --method POST /repos/eigies/eiti/actions/runners/registration-token | ConvertFrom-Json
if (-not $response.token) { throw 'Empty token in response' }
Write-Ok 'Token obtained'

# --- Step 5: Install runner ---
Write-Step 'Installing GitHub Actions runner'
$setupRunner = Join-Path $PSScriptRoot '..\..\deploy-lan-iis\scripts\setup-runner.ps1'
& $setupRunner -Token $response.token

Write-Host "`n=== CI/CD setup complete ===" -ForegroundColor Green
Write-Host 'Push to main to trigger the first deploy.'
Write-Host 'Monitor deploys at: https://github.com/eigies/eiti/actions'
