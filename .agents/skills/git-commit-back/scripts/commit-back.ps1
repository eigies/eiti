param(
    [string]$Message,
    [string]$RepoPath = 'C:\Eiti\eiti',
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
    if (($normalized | Where-Object { $_ -notlike 'eiti.tests/*' -and $_ -notlike '*test*' }).Count -eq 0) { return 'test' }
    if (($normalized | Where-Object { $_ -like 'eiti.infrastructure/migrations/*' }).Count -gt 0) { return 'feat' }
    if (($normalized | Where-Object { $_ -like 'eiti.application/features/*' -or $_ -like 'eiti.api/*' -or $_ -like 'eiti.domain/*' }).Count -gt 0) { return 'feat' }
    if (($normalized | Where-Object { $_ -like '*.sln' -or $_ -like '*.slnx' -or $_ -like '*.csproj' -or $_ -like '.gitignore' }).Count -gt 0) { return 'chore' }
    return 'chore'
}

function Get-AreaFromPath {
    param([string]$Path)

    if ($Path -match '^eiti\.Application/Features/([^/]+)/') { return $Matches[1].ToLowerInvariant() }
    if ($Path -match '^eiti\.Api/') { return 'api' }
    if ($Path -match '^eiti\.Domain/([^/]+)/') { return $Matches[1].ToLowerInvariant() }
    if ($Path -match '^eiti\.Infrastructure/Migrations/') { return 'migrations' }
    if ($Path -match '^eiti\.Infrastructure/Authentication/') { return 'auth' }
    if ($Path -match '^eiti\.Infrastructure/Persistence/') { return 'persistence' }
    if ($Path -match '^eiti\.Infrastructure/Repositories/') { return 'persistence' }
    if ($Path -match '^eiti\.Infrastructure/') { return 'infrastructure' }
    if ($Path -match '^eiti\.Tests/') { return 'tests' }
    if ($Path -match '^README\.md$') { return 'docs' }
    if ($Path -match '\.slnx?$') { return 'solution' }
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

    if ($paths.Count -eq 0) { return 'chore(back): update repository' }

    $type = Get-CommitType -Paths $paths
    $verb = Get-CommitVerb -Statuses $statuses
    $areas = $paths |
        ForEach-Object { Get-AreaFromPath -Path $_ } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Group-Object |
        Sort-Object -Property @{ Expression = 'Count'; Descending = $true }, @{ Expression = 'Name'; Descending = $false } |
        Select-Object -First 3 -ExpandProperty Name

    $subject = if ($areas.Count -gt 0) { $areas -join ', ' } else { 'repository' }
    return "$type(back): $verb $subject"
}

if (-not (Test-Path $RepoPath)) { throw "[BACK] Path not found: $RepoPath" }
$isRepo = git -C $RepoPath rev-parse --is-inside-work-tree 2>$null
if ($LASTEXITCODE -ne 0 -or $isRepo -ne 'true') { throw "[BACK] Not a valid git repo: $RepoPath" }

$name = git -C $RepoPath config user.name
$email = git -C $RepoPath config user.email
if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($email)) {
    throw "[BACK] Configure git user.name and user.email before commit."
}

if ($AllTracked) { git -C $RepoPath add -u } else { git -C $RepoPath add -A }
$staged = git -C $RepoPath diff --cached --name-only
$hasStaged = -not [string]::IsNullOrWhiteSpace(($staged -join ''))

if (([string]::IsNullOrWhiteSpace($Message) -or $AutoMessage) -and $hasStaged) {
    $Message = New-AutoCommitMessage -RepoPath $RepoPath
    Write-Host "[BACK] Generated message: $Message"
}

if (-not $hasStaged -and -not $AllowEmpty) {
    Write-Host '[BACK] No changes to commit. Skipping.'
} else {
    if ([string]::IsNullOrWhiteSpace($Message)) {
        throw '[BACK] Provide -Message or use -AutoMessage when there are staged changes.'
    }
    if ($AllowEmpty) { git -C $RepoPath commit --allow-empty -m $Message } else { git -C $RepoPath commit -m $Message }
}

if ($Push) {
    $originUrl = git -C $RepoPath remote get-url origin 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originUrl)) {
        Write-Warning '[BACK] Remote origin not found. Push skipped.'
    } else {
        $branch = git -C $RepoPath rev-parse --abbrev-ref HEAD
        if ([string]::IsNullOrWhiteSpace($branch)) { throw '[BACK] Could not resolve current branch.' }
        git -C $RepoPath push origin $branch
    }
}
