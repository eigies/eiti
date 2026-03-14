---
name: setup-cicd-runner
description: One-command CI/CD setup on the server. Installs the GitHub Actions self-hosted runner, registers it with the repo, and configures the deploy secret. Run once on the server PC after cloning the repo.
---

Run **on the server PC** (not the dev machine) as Administrator:

```powershell
& "C:\eiti\.agents\skills\setup-cicd-runner\scripts\setup-cicd.ps1" `
    -ConnectionString "<connection-string>" `
    -GhToken "<github-pat>"
```

After this, every push to `main` on the dev machine automatically deploys to IIS.

## What it does automatically

1. Installs `gh` CLI via winget if missing
2. Authenticates with GitHub using the provided PAT
3. Sets `DEPLOY_CONNECTION_STRING` as a GitHub Actions secret
4. Generates a runner registration token via the GitHub API
5. Downloads, installs, and starts the self-hosted runner as a Windows service (LocalSystem)

## Parameters

| Parameter | Required | Description |
|---|---|---|
| `-ConnectionString` | Yes | Production DB connection string |
| `-GhToken` | No | GitHub Personal Access Token. Skip if already authenticated via `gh auth login` |

## GitHub PAT requirements

The PAT needs these scopes:
- `repo` — to register the runner and set secrets

Generate one at: https://github.com/settings/tokens

## Prerequisites on the server

- Windows with IIS already configured (run `deploy-lan-iis/scripts/precheck.ps1` first if needed)
- Internet access to reach github.com
- The frontend repo already cloned at `C:\eiti-front`

## After setup

- Runner appears at: https://github.com/eigies/eiti/settings/actions/runners
- Deploy logs at: https://github.com/eigies/eiti/actions
- To re-register the runner (e.g. after OS reinstall), just run this script again
