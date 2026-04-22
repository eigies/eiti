[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$NgrokExe,
    [Parameter(Mandatory)]
    [string]$ConfigPath,
    [Parameter(Mandatory)]
    [string]$StdoutLog,
    [Parameter(Mandatory)]
    [string]$StderrLog
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $NgrokExe)) {
    throw "ngrok executable not found at '$NgrokExe'."
}

if (-not (Test-Path $ConfigPath)) {
    throw "ngrok config not found at '$ConfigPath'."
}

New-Item -ItemType Directory -Path (Split-Path -Path $StdoutLog -Parent) -Force | Out-Null

# Keep ngrok as the foreground process for the scheduled task so the tunnel
# survives the GitHub Actions job and Task Scheduler can report failures.
& $NgrokExe start --all --config $ConfigPath --log stdout --log-format json 1>> $StdoutLog 2>> $StderrLog
exit $LASTEXITCODE
