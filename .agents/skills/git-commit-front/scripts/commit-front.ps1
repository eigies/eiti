param(
    [string]$Message,
    [string]$RepoPath = 'C:\EiTeFront\eiti-front',
    [switch]$Push,
    [switch]$AllTracked,
    [switch]$AllowEmpty,
    [switch]$AutoMessage
)

$ErrorActionPreference = 'Stop'

function Get-CommitVerb {
    param([string[]]$Statuses)

    $unique = $Statuses | Where-Object { $_ } | Sort-Object -Unique
    if ($unique.Count -eq 1 -and $unique[0] -eq 'A') { return 'add' }
    if ($unique.Count -eq 1 -and $unique[0] -eq 'D') { return 'remove' }
    if ($unique -contains 'R') { return 'rename' }
    return 'update'
}

function Get-CommitType {
    param([string[]]$Paths)

    if (-not $Paths -or $Paths.Count -eq 0) { return 'chore' }

    $normalized = $Paths | ForEach-Object { $_.ToLowerInvariant() }
    if (($normalized | Where-Object { $_ -notmatch '(^|/)(readme\.md|docs?/|.+\.md$)' }).Count -eq 0) { return 'docs' }
    if (($normalized | Where-Object { $_ -notlike '*.spec.ts' -and $_ -notlike '*test*' }).Count -eq 0) { return 'test' }
    if (($normalized | Where-Object { $_ -like 'src/app/features/*' -or $_ -like 'src/app/shared/components/*' }).Count -gt 0) { return 'feat' }
    if (($normalized | Where-Object { $_ -like 'src/app/core/*' }).Count -gt 0) { return 'refactor' }
    if (($normalized | Where-Object { $_ -eq 'package.json' -or $_ -eq 'package-lock.json' -or $_ -eq 'angular.json' }).Count -gt 0) { return 'chore' }
    return 'chore'
}

function Get-AreaFromPath {
    param([string]$Path)

    if ($Path -match '^src/app/features/([^/]+)/') { return $Matches[1].ToLowerInvariant() }
    if ($Path -match '^src/app/shared/components/([^/]+)/') { return $Matches[1].ToLowerInvariant() }
    if ($Path -match '^src/app/core/services/([^/]+)\.service\.ts$') { return $Matches[1].ToLowerInvariant() }
    if ($Path -match '^src/app/core/models/([^/]+)\.models\.ts$') { return $Matches[1].ToLowerInvariant() }
    if ($Path -match '^src/assets/') { return 'assets' }
    if ($Path -match '^src/styles\.css$') { return 'styles' }
    if ($Path -match '^package(-lock)?\.json$') { return 'dependencies' }
    if ($Path -match '^angular\.json$') { return 'build' }
    if ($Path -match '^README\.md$') { return 'docs' }
    return [System.IO.Path]::GetFileNameWithoutExtension($Path).ToLowerInvariant()
}

function New-AutoCommitMessage {
    param([string]$RepoPath)

    $entries = git -C $RepoPath diff --cached --name-status --find-renames
    if (-not $entries) { return $null }

    $statuses = New-Object System.Collections.Generic.List[string]
    $paths = New-Object System.Collections.Generic.List[string]

    foreach ($entry in $entries) {
        $parts = $entry -split "\s+"
        if ($parts.Count -lt 2) { continue }

        $status = $parts[0]
        if ($status.StartsWith('R')) {
            $statuses.Add('R')
            $paths.Add($parts[$parts.Count - 1])
            continue
        }

        $statuses.Add($status.Substring(0, 1))
        $paths.Add($parts[1])
    }

    if ($paths.Count -eq 0) { return 'chore(front): update repository' }

    $type = Get-CommitType -Paths $paths
    $verb = Get-CommitVerb -Statuses $statuses
    $areas = $paths |
        ForEach-Object { Get-AreaFromPath -Path $_ } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Group-Object |
        Sort-Object -Property @{ Expression = 'Count'; Descending = $true }, @{ Expression = 'Name'; Descending = $false } |
        Select-Object -First 3 -ExpandProperty Name

    $subject = if ($areas.Count -gt 0) { $areas -join ', ' } else { 'repository' }
    return "$type(front): $verb $subject"
}

if (-not (Test-Path $RepoPath)) { throw "[FRONT] Path not found: $RepoPath" }
$isRepo = git -C $RepoPath rev-parse --is-inside-work-tree 2>$null
if ($LASTEXITCODE -ne 0 -or $isRepo -ne 'true') { throw "[FRONT] Not a valid git repo: $RepoPath" }

$name = git -C $RepoPath config user.name
$email = git -C $RepoPath config user.email
if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($email)) {
    throw "[FRONT] Configure git user.name and user.email before commit."
}

if ($AllTracked) { git -C $RepoPath add -u } else { git -C $RepoPath add -A }
$staged = git -C $RepoPath diff --cached --name-only
$hasStaged = -not [string]::IsNullOrWhiteSpace(($staged -join ''))

if (([string]::IsNullOrWhiteSpace($Message) -or $AutoMessage) -and $hasStaged) {
    $Message = New-AutoCommitMessage -RepoPath $RepoPath
    Write-Host "[FRONT] Generated message: $Message"
}

if (-not $hasStaged -and -not $AllowEmpty) {
    Write-Host '[FRONT] No changes to commit. Skipping.'
} else {
    if ([string]::IsNullOrWhiteSpace($Message)) {
        throw '[FRONT] Provide -Message or use -AutoMessage when there are staged changes.'
    }
    if ($AllowEmpty) { git -C $RepoPath commit --allow-empty -m $Message } else { git -C $RepoPath commit -m $Message }
}

if ($Push) {
    $originUrl = git -C $RepoPath remote get-url origin 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originUrl)) {
        Write-Warning '[FRONT] Remote origin not found. Push skipped.'
    } else {
        $branch = git -C $RepoPath rev-parse --abbrev-ref HEAD
        if ([string]::IsNullOrWhiteSpace($branch)) { throw '[FRONT] Could not resolve current branch.' }
        git -C $RepoPath push origin $branch
    }
}
