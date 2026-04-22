# Agustin CLAUDE.md File

## Workflow Orchestration

### 1. Plan Mode Default

- Enter plan mode for ANY non-trivial task (3+ steps or architectural decisions)
- If something goes sideways, STOP and re-plan immediately
- Use plan mode for verification steps, not just building
- Write detailed specs upfront to reduce ambiguity

### 2. Subagent Strategy — Always Parallel Team Agents

- **Default to parallel team agents**: for any task with 2+ independent workstreams, launch all agents simultaneously in a single message
- Use `subagent_type: "Explore"` for codebase research, `"general-purpose"` for implementation
- Run background agents (`run_in_background: true`) for independent tasks; wait for results only when there's a dependency
- Each agent gets a focused, complete prompt — include build/verify steps explicitly so agents don't skip them
- **Frontend agents MUST end with**: `cd C:/EiTeFront/eiti-front && ng build --configuration development` and report errors
- **Backend agents MUST end with**: `dotnet build eiti.Application/eiti.Application.csproj` (with dependencies) and report errors
- Split work by domain boundary: e.g., one agent on backend feature, another on frontend feature — they can work simultaneously
- After agents complete, consolidate results in main context; don't duplicate work agents already did

### 3. Self-Improvement Loop

- After ANY correction from the user: update tasks/lessons.md with the pattern
- Write rules for yourself that prevent the same mistake
- Ruthlessly iterate on these lessons until mistake rate drops
- Review lessons at session start for relevant project

### 4. Verification Before Done

- Never mark a task complete without proving it works
- Diff behavior between main and your changes when relevant
- Ask yourself: "Would a staff engineer approve this?"
- Run tests, check logs, demonstrate correctness
- **Frontend tasks MUST compile before delivery**: Run `cd C:/EiTeFront/eiti-front && ng build --configuration development` as the final step. A task is NOT done if the build fails. This applies to subagents too — always include the build command explicitly in subagent instructions.

### 5. Demand Elegance (Balanced)

- For non-trivial changes: pause and ask "is there a more elegant way?"
- If a fix feels hacky: "Knowing everything I know now, implement the elegant solution"
- Skip this for simple, obvious fixes -- don't over-engineer
- Challenge your own work before presenting it

### 6. Autonomous Bug Fixing

- When given a bug report: just fix it. Don't ask for hand-holding
- Point at logs, errors, failing tests -- then resolve them
- Zero context switching required from the user
- Go fix failing CI tests without being told how

---

## Task Management

1. Plan First: Write plan to tasks/todo.md with checkable items  
2. Verify Plan: Check in before starting implementation  
3. Track Progress: Mark items complete as you go  
4. Explain Changes: High-level summary at each step  
5. Document Results: Add review section to tasks/todo.md  
6. Capture Lessons: Update tasks/lessons.md after corrections  

---

## Core Principles

- **Simplicity First**: Make every change as simple as possible. Impact minimal code.
- **No Laziness**: Find root causes. No temporary fixes. Senior developer standards.
- **Minimal Impact**: Only touch what's necessary. No side effects with new bugs.

---

## Frontend (Angular) — C:/EiTeFront

### Design & UI

- Antes de implementar cualquier componente o pantalla nueva, invocar la skill `/frontend-design`
- La skill define las decisiones visuales (layout, colores, espaciado, interacciones) antes de escribir código
- No saltear este paso aunque el cambio parezca pequeño — la consistencia visual depende de seguirlo siempre

### Stack

- Angular 16 · Standalone components · RxJS 7 · TypeScript strict
- No NgRx — state via `BehaviorSubject` in services
- Styling: plain CSS with custom properties (no CSS framework)
- Exports: jsPDF · XLSX

### Architecture Rules

- **Feature-first folders**: every feature lives in `src/app/features/<name>/`
- **Core vs Shared**: business services/guards/interceptors → `core/`; reusable UI components/services → `shared/`
- **Standalone only**: never create NgModules. All components, directives, and pipes must be `standalone: true`
- **Lazy loading**: every feature route must be lazy-loaded via `loadComponent` or `loadChildren`
- **Models**: all interfaces and enums go in `core/models/<domain>.models.ts`

### Component Rules

- Use `OnPush` change detection for any component that only depends on inputs or observables
- Prefer the `async` pipe over manual subscriptions in templates — avoids memory leaks
- When manual subscription is unavoidable, unsubscribe via `takeUntilDestroyed()` or `DestroyRef`
- Keep components thin: delegate all business logic and HTTP calls to services
- One component selector prefix: `app-*`

### Service Rules

- All HTTP services live in `core/services/` and are `providedIn: 'root'`
- Use typed `HttpClient` responses: `this.http.get<MyType>(url)`
- Build URLs from `environment.apiUrl` — never hardcode endpoints
- Surface errors to the user via `ToastService`, not `console.error` alone
- Use `forkJoin` for parallel requests; `switchMap`/`mergeMap` for dependent chains

### State & Reactivity

- Simple reactive state: `BehaviorSubject` + exposed `Observable` (no setter access from outside)
- Do not store raw API responses in components — map to view models when shapes differ
- Avoid nested subscribes; compose with RxJS operators instead

### Forms

- Always use **Reactive Forms** (`FormBuilder`, `FormGroup`, `Validators`) — no Template-driven forms
- Validate on the service boundary too, not only client-side
- Reuse the existing `isInvalid(field)` helper pattern for template error display

### Permissions & Guards

- Every route that requires access control must declare `data: { permission: PermissionCodes.X }`
- Use `PermissionCodes` constants — never raw permission strings in route files
- Re-check permissions reactively if the user's role can change mid-session

### Styling

- Use CSS custom properties (`--var-name`) for all colors, spacing, and fonts
- Support both `theme-dark` / `theme-light` classes — never hardcode color values
- Component styles are scoped; global rules go in `src/styles.css` only when truly global
- Follow `.editorconfig`: 2-space indent, single quotes, no trailing whitespace

### TypeScript

- Strict mode is on — no `any`, no `!` non-null assertions without a comment explaining why
- Use `readonly` for properties that should not be reassigned after init
- Export interfaces from model files; do not inline types in service or component files

### Naming Conventions

| Artifact | Convention |
|---|---|
| Component | `feature-name.component.ts` |
| Service | `feature-name.service.ts` |
| Model file | `domain.models.ts` |
| Observable variable | suffix `$` (e.g., `users$`) |
| Private subject | prefix `_` + suffix `$` (e.g., `_users$`) |
| Selector | `app-feature-name` |

### What to Avoid

- `NgModules` — the project is fully standalone
- `any` type — always define a proper interface
- `localStorage` access outside `AuthService` or `ThemeService`
- Hardcoded API URLs
- Nested subscriptions
- Template-driven forms
- Business logic inside components

---

## Backend (.NET 8) — C:/Eiti/eiti

### Stack

- .NET 8 · Clean Architecture · Vertical Slice · MediatR · FluentValidation · EF Core · SQL Server
- Pattern: `Result<T>` (never throw for business errors) · `ICurrentUserService` for auth context
- Error types: `Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`

### Architecture Rules

- **Vertical Slice**: every feature lives in `eiti.Application/Features/<Domain>/<Command|Query>/<Feature>/`
- **One handler per file**: `Handler.cs`, `Command.cs`/`Query.cs`, `Validator.cs`, `Response.cs`, `Errors.cs`
- **Errors file**: extract all `static readonly Error` constants into a `<Feature>Errors.cs` — never inline in handler
- **No exceptions for business logic**: use `Result.Failure(Error.X)` and let `ResultExtensions` map to HTTP
- **`GlobalExceptionHandlingMiddleware`** handles only truly unexpected exceptions

### Handler Rules

- Start every handler with auth check: `var authResult = _currentUserService.EnsureAuthenticated();` (or `EnsureAuthenticatedWithContext()` if UserId is needed)
- Capture `CompanyId`/`UserId` AFTER the auth check with `!` null-forgiving (auth guarantees non-null)
- Max ~10 constructor dependencies per handler — if more, extract a domain service
- Call `await _unitOfWork.SaveChangesAsync(cancellationToken)` as the last step before building the response

### Validator Rules

- Validators live beside their command/query
- Use `Enum.IsDefined(typeof(EnumType), value)` — never `InclusiveBetween(1, N)` for enum validation
- Validators with cross-cutting business checks can inject repositories via constructor (FluentValidation DI)

### Domain Rules

- Domain entities expose behavior via methods, not public setters
- Collections: `private readonly List<T> _items = []` + `public IReadOnlyCollection<T> Items => _items.AsReadOnly()`
- Value Objects validate in constructor and throw `ArgumentException` (domain layer, never null)
- Aggregate root methods enforce invariants and return `void` (invariant violations throw — they're programming errors, not user errors)

### Infrastructure Rules

- Repositories implement only what handlers need — no generic `GetAll()` that bypasses business filtering
- EF configurations: `IEntityTypeConfiguration<T>` per entity, `decimal(18,2)` for money, explicit `HasIndex` on FK columns
- Build migrations from `eiti.Infrastructure` directly when the API process has DLLs locked
- Always build WITH dependencies: `dotnet build eiti.Application/eiti.Application.csproj` — `--no-dependencies` uses stale cached DLLs

### Permissions & Authorization

- All new features need permission codes in `PermissionCodes.cs` and role assignments in `RoleCatalog.cs`
- Use `IRequirePermissions` on commands/queries that need permission checks
- `AuthorizationBehavior` runs before `ValidationBehavior` in the pipeline

### Naming Conventions

| Artifact | Convention |
|---|---|
| Command/Query | `<Action><Domain>Command.cs` |
| Handler | `<Action><Domain>Handler.cs` |
| Response | `<Action><Domain>Response.cs` |
| Errors | `<Action><Domain>Errors.cs` |
| Repository interface | `I<Domain>Repository.cs` |
| Error constant | `public static readonly Error NotFound = Error.NotFound("Domain.NotFound", "...")` |

### What to Avoid

- `throw` for business rule violations — use `Result.Failure`
- Inline error strings in handlers — always use `*Errors.cs` constants
- `InclusiveBetween(1, N)` for enum validation — use `Enum.IsDefined`
- Bare `catch` blocks that swallow exceptions without logging
- Mutations on domain entity properties from outside the aggregate
- Running `dotnet build` with `--no-dependencies` for final verification

