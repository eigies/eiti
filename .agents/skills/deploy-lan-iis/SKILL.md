---
name: deploy-lan-iis
description: Deploy Eiti API, frontend, and database migrations to a Windows IIS server on LAN. Use when setting up or updating a LAN-accessible environment with front at root and API under /api.
---

Use scripts in this skill:
- `scripts/precheck.ps1`
- `scripts/deploy.ps1`
- `scripts/start-ngrok-tunnel.ps1`
- `scripts/send-telegram.ps1`
- `scripts/start-quick-tunnel.ps1`

Run from `C:\Eiti\eiti`:

```powershell
& "C:\Eiti\eiti\.agents\skills\deploy-lan-iis\scripts\deploy.ps1" -HostHeader "eiti.interno.local" -Port 80 -ConnectionString "<connection-string>"
```

The deploy script auto-detects both layouts:
- backend in `C:\Eiti\eiti` with frontend in `C:\EiTeFront\eiti-front`
- backend in `C:\eiti` with frontend in `C:\eiti-front`

If needed, you can still override paths explicitly with `-ApiProjectPath` and `-FrontRootPath`.

Apply this flow:
1. Run precheck.
2. Build frontend.
3. Publish API.
4. Run migrations.
5. Configure IIS site and `/api` app.
6. Open firewall rule.
7. Run smoke tests.
8. Optional for external testers without router changes: start a free Cloudflare Quick Tunnel.

Quick Tunnel notes:
- Run `scripts/start-quick-tunnel.ps1` after a successful deploy when the user wants internet access for testing.
- It downloads `cloudflared` to `C:\eiti\tools\cloudflared` if needed and exposes the local IIS site on port 80.
- It returns a `https://*.trycloudflare.com` URL.
- The URL is temporary and usually changes if the process stops or the PC restarts.
- This is suitable for testing, not for stable production access.

## CI/CD: Auto-deploy on push to main

Push to `main` → GitHub Actions → self-hosted runner on the server → `deploy.ps1` runs automatically.

### One-time setup on the server PC

**1. Get a runner registration token**
Go to: https://github.com/eigies/eiti/settings/actions/runners → New self-hosted runner → copy the token.

**2. Run setup-runner.ps1 as Administrator**

```powershell
& "C:\eiti\.agents\skills\deploy-lan-iis\scripts\setup-runner.ps1" -Token "<token-from-github>"
```

This downloads the runner, registers it with labels `self-hosted,windows,iis`, installs it as a Windows service running as `LocalSystem` (required for IIS), and starts it.

The runner registration token expires in 1 hour — generate it right before running the script.

**3. Add the GitHub secret**
Go to: https://github.com/eigies/eiti/settings/secrets/actions → New repository secret.
- Name: `DEPLOY_CONNECTION_STRING`
- Value: the production connection string

**4. Ensure git credentials for the frontend repo**
The workflow runs `git fetch/reset` on `C:\eiti-front`. The server needs credentials configured to pull that repo (SSH key or Windows Credential Manager).

### How it works

- Workflow file: `.github/workflows/deploy-lan-iis.yml`
- Triggers on push to `main` only
- The runner checks out the backend into its workspace
- Updates `C:\eiti-front` to `origin/main` (hard reset, no merge conflicts)
- Calls `deploy.ps1 -SkipPrecheck` — precheck skipped since the server is already set up
- Deploy logs visible at: https://github.com/eigies/eiti/actions

### Server paths assumed by the workflow

| Resource | Path |
|---|---|
| Frontend source | `C:\eiti-front` |
| API publish | `C:\inetpub\eiti\api` |
| Front publish | `C:\inetpub\eiti\front` |

### Credentials Telegram
Store these as repository secrets instead of plaintext:
- `TELEGRAM_BOT_TOKEN`
- `TELEGRAM_CHAT_ID`
- `NGROK_AUTHTOKEN`
