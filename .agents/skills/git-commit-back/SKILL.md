---
name: git-commit-back
description: Commit changes only in the eiti backend repository with a custom commit message, with optional push to origin. Use when backend changes must be committed independently.
---

Use `scripts/commit-back.ps1`.

Run from `C:\Eiti\eiti`:

```powershell
powershell -ExecutionPolicy Bypass -File .\.agents\skills\git-commit-back\scripts\commit-back.ps1 -Message "feat(back): ..."
```

Optional push:

```powershell
powershell -ExecutionPolicy Bypass -File .\.agents\skills\git-commit-back\scripts\commit-back.ps1 -Message "feat(back): ..." -Push
```

Automatic message from the staged diff:

```powershell
powershell -ExecutionPolicy Bypass -File .\.agents\skills\git-commit-back\scripts\commit-back.ps1 -AutoMessage
```

Automatic message and push:

```powershell
powershell -ExecutionPolicy Bypass -File .\.agents\skills\git-commit-back\scripts\commit-back.ps1 -AutoMessage -Push
```
