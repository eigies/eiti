[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RepoPath,
    [Parameter(Mandatory)]
    [string]$FromSha,
    [string]$ToSha = 'HEAD',
    [switch]$Detailed
)

$ErrorActionPreference = 'Stop'

# Get commits between previous deploy and current HEAD
$logFormat = if ($Detailed) { "%h %s" } else { "%s" }
$logOutput = git -C $RepoPath log "$FromSha..$ToSha" --pretty=format:"$logFormat" 2>&1

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($logOutput)) {
    return "Sin cambios registrados."
}

$commits = $logOutput -split "`n" | Where-Object { $_ -match '\S' }

if ($commits.Count -eq 0) {
    return "Sin cambios registrados."
}

# Detailed mode: all commits grouped, with SHA prefix
if ($Detailed) {
    $groups = @{
        feat    = [System.Collections.Generic.List[string]]::new()
        fix     = [System.Collections.Generic.List[string]]::new()
        chore   = [System.Collections.Generic.List[string]]::new()
        other   = [System.Collections.Generic.List[string]]::new()
    }

    foreach ($commit in $commits) {
        $sha     = $commit.Substring(0, 7)
        $subject = $commit.Substring(8).Trim()
        $entry   = "[$sha] $subject"

        if ($subject -match '^feat(?:\([^)]+\))?!?:') {
            $groups.feat.Add($entry)
        } elseif ($subject -match '^fix(?:\([^)]+\))?!?:') {
            $groups.fix.Add($entry)
        } elseif ($subject -match '^(?:chore|refactor|perf|style|test|docs|ci|build)(?:\([^)]+\))?!?:') {
            $groups.chore.Add($entry)
        } else {
            $groups.other.Add($entry)
        }
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("Commits ($($commits.Count) total):")

    foreach ($key in @('feat','fix','chore','other')) {
        if ($groups[$key].Count -gt 0) {
            $lines.Add("")
            $label = @{ feat='feat'; fix='fix'; chore='chore/ci/refactor'; other='sin prefijo' }[$key]
            $lines.Add("[$label]")
            foreach ($item in $groups[$key]) { $lines.Add("  $item") }
        }
    }

    return $lines -join "`n"
}

# Client mode: grouped, clean, no SHA, skip chore/ci noise
$groups = @{
    feat  = [System.Collections.Generic.List[string]]::new()
    fix   = [System.Collections.Generic.List[string]]::new()
    other = [System.Collections.Generic.List[string]]::new()
}

foreach ($commit in $commits) {
    if ($commit -match '^feat(?:\([^)]+\))?!?:\s*(.+)$') {
        $groups.feat.Add($Matches[1].Trim())
    } elseif ($commit -match '^fix(?:\([^)]+\))?!?:\s*(.+)$') {
        $groups.fix.Add($Matches[1].Trim())
    } elseif ($commit -match '^(?:chore|refactor|perf|style|test|docs|ci|build)(?:\([^)]+\))?!?:\s*(.+)$') {
        # skip — ruido interno, no relevante para el cliente
    } else {
        $groups.other.Add($commit.Trim())
    }
}

$lines = [System.Collections.Generic.List[string]]::new()

if ($groups.feat.Count -gt 0) {
    $lines.Add("Novedades:")
    foreach ($item in $groups.feat) { $lines.Add("  + $item") }
}

if ($groups.fix.Count -gt 0) {
    if ($lines.Count -gt 0) { $lines.Add("") }
    $lines.Add("Correcciones:")
    foreach ($item in $groups.fix) { $lines.Add("  * $item") }
}

if ($groups.other.Count -gt 0) {
    if ($lines.Count -gt 0) { $lines.Add("") }
    $lines.Add("Otros cambios:")
    foreach ($item in $groups.other) { $lines.Add("  - $item") }
}

if ($lines.Count -eq 0) {
    return "Actualizaciones internas sin cambios visibles."
}

return $lines -join "`n"
