---
name: git-commit-front
description: Commit changes only in the eiti-front repository with a custom commit message, with optional push to origin. Use when front-end changes must be committed independently.
---

Use `scripts/commit-front.ps1`.

Run from `C:\Eiti\eiti`:

```powershell
powershell -ExecutionPolicy Bypass -File .\.agents\skills\git-commit-front\scripts\commit-front.ps1 -Message "feat(front): ..."
```

Optional push:

```powershell
powershell -ExecutionPolicy Bypass -File .\.agents\skills\git-commit-front\scripts\commit-front.ps1 -Message "feat(front): ..." -Push
```

Automatic message from the staged diff:

```powershell
powershell -ExecutionPolicy Bypass -File .\.agents\skills\git-commit-front\scripts\commit-front.ps1 -AutoMessage
```

Automatic message and push:

```powershell
powershell -ExecutionPolicy Bypass -File .\.agents\skills\git-commit-front\scripts\commit-front.ps1 -AutoMessage -Push
```
