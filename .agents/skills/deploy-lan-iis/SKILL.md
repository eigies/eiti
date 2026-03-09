---
name: deploy-lan-iis
description: Deploy Eiti API, frontend, and database migrations to a Windows IIS server on LAN. Use when setting up or updating a LAN-accessible environment with front at root and API under /api.
---

Use scripts in this skill:
- `scripts/precheck.ps1`
- `scripts/deploy.ps1`

Run from `C:\Eiti\eiti`:

```powershell
& "C:\Eiti\eiti\.agents\skills\deploy-lan-iis\scripts\deploy.ps1" -HostHeader "eiti.interno.local" -Port 80 -ConnectionString "<connection-string>"
```

Apply this flow:
1. Run precheck.
2. Build frontend.
3. Publish API.
4. Run migrations.
5. Configure IIS site and `/api` app.
6. Open firewall rule.
7. Run smoke tests.
