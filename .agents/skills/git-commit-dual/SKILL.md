---
name: git-commit-dual
description: Commit changes in eiti-front and eiti as separate commits using one shared commit message, with optional push to origin. Use when the user wants one message applied to both repositories.
---

Use `scripts/commit-dual.ps1`.

Run from `C:\Eiti\eiti`:

```powershell
powershell -ExecutionPolicy Bypass -File .\.agents\skills\git-commit-dual\scripts\commit-dual.ps1 -Message "tu mensaje"
```

Optional push:

```powershell
powershell -ExecutionPolicy Bypass -File .\.agents\skills\git-commit-dual\scripts\commit-dual.ps1 -Message "tu mensaje" -Push
```
