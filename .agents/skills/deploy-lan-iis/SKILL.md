---
name: deploy-lan-iis
description: Deploy Eiti API, frontend, and database migrations to a Windows IIS server on LAN. Use when setting up or updating a LAN-accessible environment with front at root and API under /api.
---

Use scripts in this skill:
- `scripts/precheck.ps1`
- `scripts/deploy.ps1`
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
