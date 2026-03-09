param(
    [Parameter(Mandatory = $true)]
    [string]$Message,

    [string]$FrontRepoPath = 'C:\EiTeFront\eiti-front',
    [string]$BackRepoPath = 'C:\Eiti\eiti',

    [switch]$Push,
    [switch]$AllTracked,
    [switch]$AllowEmpty
)

$ErrorActionPreference = 'Stop'

function Assert-GitRepo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoPath,
        [Parameter(Mandatory = $true)]
        [string]$RepoLabel
    )

    if (-not (Test-Path $RepoPath)) {
        throw "[$RepoLabel] Ruta no encontrada: $RepoPath"
    }

    $isRepo = git -C $RepoPath rev-parse --is-inside-work-tree 2>$null
    if ($LASTEXITCODE -ne 0 -or $isRepo -ne 'true') {
        throw "[$RepoLabel] No es un repo git válido: $RepoPath"
    }
}

function Assert-GitIdentity {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoPath,
        [Parameter(Mandatory = $true)]
        [string]$RepoLabel
    )

    $name = git -C $RepoPath config user.name
    $email = git -C $RepoPath config user.email

    if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($email)) {
        throw "[$RepoLabel] Configurá git user.name y user.email antes de commitear."
    }
}

function Commit-Repo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoPath,
        [Parameter(Mandatory = $true)]
        [string]$RepoLabel
    )

    Write-Host "[$RepoLabel] Preparando stage..."
    if ($AllTracked) {
        git -C $RepoPath add -u
    } else {
        git -C $RepoPath add -A
    }

    $staged = git -C $RepoPath diff --cached --name-only
    if ([string]::IsNullOrWhiteSpace(($staged -join ''))) {
        if ($AllowEmpty) {
            Write-Host "[$RepoLabel] Sin cambios staged. Creando commit vacío (-AllowEmpty)."
            git -C $RepoPath commit --allow-empty -m $Message
        } else {
            Write-Host "[$RepoLabel] Sin cambios para commitear. Se omite."
            return
        }
    } else {
        Write-Host "[$RepoLabel] Commit en progreso..."
        git -C $RepoPath commit -m $Message
    }

    if ($Push) {
        $originUrl = git -C $RepoPath remote get-url origin 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originUrl)) {
            Write-Warning "[$RepoLabel] No hay remoto 'origin'. Se omite push."
            return
        }

        $branch = git -C $RepoPath rev-parse --abbrev-ref HEAD
        if ([string]::IsNullOrWhiteSpace($branch)) {
            throw "[$RepoLabel] No se pudo determinar rama actual para push."
        }

        Write-Host "[$RepoLabel] Push a origin/$branch ..."
        git -C $RepoPath push origin $branch
    }
}

Write-Host 'Validando repositorios...'
Assert-GitRepo -RepoPath $FrontRepoPath -RepoLabel 'FRONT'
Assert-GitRepo -RepoPath $BackRepoPath -RepoLabel 'BACK'

Write-Host 'Validando identidad git...'
Assert-GitIdentity -RepoPath $FrontRepoPath -RepoLabel 'FRONT'
Assert-GitIdentity -RepoPath $BackRepoPath -RepoLabel 'BACK'

Commit-Repo -RepoPath $FrontRepoPath -RepoLabel 'FRONT'
Commit-Repo -RepoPath $BackRepoPath -RepoLabel 'BACK'

Write-Host 'Proceso finalizado.'
