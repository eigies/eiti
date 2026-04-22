# Lessons Learned

## Transport Assignment Bugs - 2026-03-22

### Bug 1 — 500 on reassignment after QUITAR

**Root cause:** `SaleTransportAssignments` has a unique index on `SaleId` (one row per sale). QUITAR calls `assignment.Cancel()` (status → Cancelled, row stays in DB) + `sale.ClearTransportAssignment()` (clears `sale.TransportAssignmentId`). When the user tries to create a new transport via POST, `CreateSaleTransportHandler` finds the Cancelled record, passes the `existing.Status != Cancelled` guard, then tries to `AddAsync()` a brand-new row → unique constraint violation → 500.

**Fix:** In `CreateSaleTransportHandler`, when the existing record is Cancelled, call `existing.Reassign()` (new domain method) instead of creating a new entity. `Reassign()` resets Status → Assigned, updates Driver/Vehicle/Notes, clears DispatchedAt/DeliveredAt, updates AssignedAt. Skip `AddAsync()` since the row already exists. Still call `sale.AssignTransport(assignment.Id)` to re-link the sale.

**Pattern to remember:** If a domain entity has a unique-per-parent constraint, "soft deleting" (marking Cancelled) + "recreating" will always fail. The handler must detect the soft-deleted record and reactivate it (upsert semantics).

---

### Bug 2 — Chip shows "Pendiente" after QUITAR (should show "Cancelado")

**Root cause:** `ListBySaleIdsAsync` returns ALL assignments (no status filter), including Cancelled ones. `ListSalesHandler` builds `assignmentMap` from these. After QUITAR: `sale.TransportAssignmentId = null` (cleared) but the Cancelled assignment is still in `assignmentMap`, so `TransportStatus = 4` ends up in the response. In `transportStatusChipLabel()`, the `!transportAssignmentId` branch only checked for null and returned "Pendiente", ignoring the `transportStatus = 4` that was already present in the response.

**Fix:** Frontend only — in `transportStatusChipLabel()` and `transportStatusChipClass()`, when `!sale.transportAssignmentId` AND `sale.transportStatus === 4`, return "Cancelado" / `chip--transport-cancelled` instead of "Pendiente" / `chip--transport-pending`. The `!hasDelivery → 'No aplica'` check at the top already handles Retiro sales correctly.

**Pattern to remember:** When the backend has a soft-delete pattern (Cancelled row stays, FK cleared on parent), the list query will still return the orphaned record in bulk fetches. The frontend must defensively handle states where `assignmentId = null` but `transportStatus` is still present — don't assume a null ID means "never assigned".

## Agent-UserServiceExt - 2026-03-17

### Cambios realizados

- Creado `eiti.Application/Abstractions/Services/CurrentUserServiceExtensions.cs` como clase estática de extensión sobre `ICurrentUserService` con 3 métodos:
  - `EnsureAuthenticated()` - Valida solo `IsAuthenticated`, retorna `Result`
  - `EnsureAuthenticatedWithContext()` - Valida `IsAuthenticated` + `CompanyId` + `UserId`, retorna `Result`
  - `EnsureHasPermission(permission, errorCode, errorMessage)` - Valida permiso específico, retorna `Result`
- Actualizado `CloseCashSessionHandler.cs`: reemplazado check inline `!IsAuthenticated || CompanyId is null || UserId is null` por `EnsureAuthenticatedWithContext()`
- Actualizado `OpenCashSessionHandler.cs`: mismo patrón que CloseCashSession
- Actualizado `CreateSaleHandler.cs`: reemplazado `!IsAuthenticated || companyId is null` por `EnsureAuthenticated()` + check de `companyId` separado (porque el handler capturaba `companyId` antes del check y lo usaba extensamente)

### Lecciones aprendidas

- **Chequear el estado actual del archivo antes de editar**: El archivo `CreateSaleHandler.cs` había sido modificado previamente por otro agente (ya usaba `CreateSaleErrors.*` en lugar de errores inline). Intentar editar con el texto original produce error "File has been modified since read". Siempre re-leer si hay error de escritura.
- **El patrón de extensión devuelve `Result` (no lanza excepciones)**: Esto es consistente con el resto del proyecto que sigue el patrón Result/Error.
- **Warnings CS8604 son pre-existentes**: Después de refactorizar con la extension method, el compilador ya no puede inferir que `CompanyId`/`UserId` son non-null (aunque `EnsureAuthenticatedWithContext` ya los validó). Esto produce warnings CS8604. Son inherentes al diseño actual del proyecto y no son errores.
- **Hay muchos más handlers con el mismo patrón** (~60+ ocurrencias en total) que podrían beneficiarse de la misma refactorización en futuras iteraciones.

## Agent-AuthMigration - 2026-03-17

### Cambios realizados

Migrados 57 handlers (en 10 archivos) que usaban el patrón inline `if (!_currentUserService.IsAuthenticated || ...)` para usar los métodos de extensión:

**Usando `EnsureAuthenticated()` (pattern: IsAuthenticated + CompanyId only):**
- `Branches/Commands/CreateBranch/CreateBranchHandler.cs`
- `Branches/Commands/UpdateBranch/UpdateBranchHandler.cs`
- `Branches/Queries/ListBranches/ListBranchesHandler.cs`
- `CashDrawers/Commands/CreateCashDrawer/CreateCashDrawerHandler.cs`
- `CashDrawers/Commands/UpdateCashDrawer/UpdateCashDrawerHandler.cs`
- `CashDrawers/Queries/ListCashDrawers/ListCashDrawersHandler.cs`
- `CashSessions/Queries/GetCashSessionSummary/GetCashSessionSummaryHandler.cs`
- `CashSessions/Queries/GetCurrentCashSession/GetCurrentCashSessionHandler.cs`
- `CashSessions/Queries/ListCashSessionHistory/ListCashSessionHistoryHandler.cs`
- `Companies/Commands/UpdateCurrentCompany/UpdateCurrentCompanyHandler.cs`
- `Companies/Queries/GetCurrentCompany/GetCurrentCompanyHandler.cs`
- `Customers/Commands/CreateCustomer/CreateCustomerHandler.cs`
- `Customers/Commands/UpdateCustomer/UpdateCustomerHandler.cs`
- `Customers/Queries/GetCustomerById/GetCustomerByIdHandler.cs`
- `Customers/Queries/SearchCustomers/SearchCustomersHandler.cs`
- `Onboarding/Queries/GetOnboardingStatus/GetOnboardingStatusHandler.cs`
- `Products/Commands/CreateProduct/CreateProductHandler.cs`
- `Products/Commands/DeleteProduct/DeleteProductHandler.cs`
- `Products/Commands/UpdateProduct/UpdateProductHandler.cs`
- `Products/Queries/ListPagedProducts/ListPagedProductsHandler.cs`
- `Products/Queries/ListProducts/ListProductsHandler.cs`
- `Sales/Commands/DeleteSale/DeleteSaleHandler.cs`
- `Sales/Commands/SendSaleWhatsApp/SendSaleWhatsAppHandler.cs`
- `Sales/Commands/UpdateSale/UpdateSaleHandler.cs`
- `Sales/Queries/ListSales/ListSalesHandler.cs`
- `Stock/Commands/AdjustStock/AdjustStockHandler.cs`
- `Stock/Queries/GetBranchProductStock/GetBranchProductStockHandler.cs`
- `Stock/Queries/ListBranchStock/ListBranchStockHandler.cs`
- `Stock/Queries/ListStockMovements/ListStockMovementsHandler.cs`
- `Drivers/DriverFeature.cs` (3 handlers: Upsert, Get, List)
- `Employees/EmployeeFeature.cs` (6 handlers: Create, Update, Deactivate, Get, List, ListDrivers)
- `SaleTransport/SaleTransportFeature.cs` (Update, UpdateStatus, Delete, Get handlers)
- `Users/UserFeature.cs` (Create, Get, ListUsers, UpdateUserRoles handlers)
- `Vehicles/VehicleFeature.cs` (Create, Update, AssignDriver, UnassignDriver, Deactivate, Get, List, ListFleetLogs handlers)

**Usando `EnsureAuthenticatedWithContext()` (pattern: IsAuthenticated + CompanyId + UserId):**
- `CashSessions/Commands/CreateCashWithdrawal/CreateCashWithdrawalHandler.cs`
- `Onboarding/Commands/CompleteInitialCashOpen/CompleteInitialCashOpenHandler.cs`
- `SaleTransport/SaleTransportFeature.cs` (CreateSaleTransport handler)
- `Users/UserFeature.cs` (GetMyProfile, SetUserActiveStatus, ListUserRoleAudits handlers)
- `Vehicles/VehicleFeature.cs` (CreateFleetLog handler)

### Lecciones aprendidas

- **Patrón de captura previa de companyId**: Los handlers `SendSaleWhatsApp` y `UpdateSale` capturaban `var companyId = _currentUserService.CompanyId` antes del auth check. Se reemplazó conservando esa captura pero poniéndola después del `EnsureAuthenticated()` check, con el operador null-forgiving (`!`) ya que la autenticación garantiza que no es null.
- **No olvidar el semicolón en la línea de captura de variable**: `var companyId = _currentUserService.CompanyId!;` requiere `;` al final. Un typo inicial lo omitió, causando 2 errores de compilación que se corrigieron inmediatamente.
- **Los Feature files (DriverFeature, EmployeeFeature, etc.) contienen múltiples handlers**: Requieren editar múltiples bloques dentro del mismo archivo. Usar strings únicos como contexto para el Edit.
- **EnsureAuthenticated() NO valida CompanyId**: Solo valida `IsAuthenticated`. Para handlers que también necesitan CompanyId pero no UserId, se usa `EnsureAuthenticated()` (la autenticación en el sistema implica que CompanyId siempre estará disponible para usuarios autenticados). Solo cuando se necesita explícitamente UserId se usa `EnsureAuthenticatedWithContext()`.
- **Build final: 0 errores, 10 warnings** (warnings pre-existentes de vulnerabilidades en paquetes NuGet, no relacionados con estos cambios).

## Agent-MultiTask-US1-US2-US3 - 2026-03-19

### Cambios realizados

- US1: `beginEdit()` + `setEditItemPrice()` + input precio en editItems (frontend sales-page)
- US2: Fix `GetCurrentCashSessionHandler` para incluir payments en mapper + desglose de pagos en sale card expandida
- US3: Feature completa CancelSale (backend) + cancel flow frontend + cash component

### Lecciones aprendidas

- **SIEMPRE compilar el front antes de entregar una tarea**: Nunca marcar una tarea como completa sin correr `ng build` (o al menos verificar que el código TypeScript compila). Un error de tipos que tarda 2 segundos en detectarse no debe llegar al usuario. Esto aplica tanto al agente principal como a subagentes — el paso final de cualquier tarea frontend ES el build.

- **`prop?: T` vs `prop: T | undefined` rompen type predicates en strict mode**: En TypeScript strict, una propiedad opcional (`unitPriceOverride?: number`) NO es equivalente a una propiedad requerida con valor undefined (`unitPriceOverride: number | undefined`) desde la perspectiva del sistema de tipos. Cuando un `.map()` construye un objeto literal con `prop: value | undefined`, el tipo inferido tiene la prop como *required*, lo cual hace fallar el `.filter((x): x is InterfaceConPropOpcional => x !== null)`. **Fix:** agregar `as NombreDelInterface` al objeto literal en el `.map()` para que el tipo inferido sea el correcto desde el inicio. Patrón correcto: `return product ? { ...campos } as DraftItem : null;`

- **Los subagentes no compilan el front automáticamente**: Al delegar tareas frontend a subagentes, agregar explícitamente en las instrucciones: "ejecuta `cd C:/EiTeFront/eiti-front && ng build --configuration development` al final y reporta si hay errores de compilación". Sin esta instrucción, el subagente no lo hace.

## Agent-ErrorGen - 2026-03-17

### Cambios realizados

- Creados 5 archivos `*Errors.cs` con constantes `public static readonly Error`:
  - `eiti.Application/Features/Sales/Commands/CreateSale/CreateSaleErrors.cs` (8 constantes)
  - `eiti.Application/Features/Sales/Commands/UpdateSale/UpdateSaleErrors.cs` (8 constantes)
  - `eiti.Application/Features/Sales/Commands/SendSaleWhatsApp/SendSaleWhatsAppErrors.cs` (11 constantes)
  - `eiti.Application/Features/CashSessions/Commands/OpenCashSession/OpenCashSessionErrors.cs` (3 constantes)
  - `eiti.Application/Features/CashSessions/Commands/CloseCashSession/CloseCashSessionErrors.cs` (3 constantes)
- Actualizados los 5 handlers correspondientes para reemplazar todos los errores inline con sus constantes estáticas.
- Errores con mensajes dinámicos (usando `ex.Message` de excepciones) conservados como inline ya que no pueden representarse como `static readonly`.

### Lecciones aprendidas

- **Errores con mensajes dinámicos no pueden ser static readonly**: Cuando el mensaje del error incluye `ex.Message` o interpolaciones con datos de runtime, no se puede extraer a una constante. Estos deben permanecer inline.
- **El linter/IDE aplica cambios concurrentemente**: Mientras se editan los handlers, el linter puede aplicar cambios paralelos (p.ej., reemplazando auth checks con `EnsureAuthenticated()`). Siempre re-leer antes de editar si hay error "File has been modified since read".
- **Placement consistente con Auth**: Los archivos `*Errors.cs` se crean en el mismo directorio que el handler que los usa (no en un Common separado), siguiendo el patrón ya establecido en Auth (RegisterErrors.cs está en Commands/Register/, LoginErrors.cs en Queries/Login/).
- **dotnet build pasa sin errores**: Las warnings CS8604 son pre-existentes y no relacionadas con estos cambios.

## Agent-Beta - ResultExtensions Deduplication - 2026-03-20

### Cambios realizados

- Refactorizado `eiti.Api/Extensions/ResultExtensions.cs`: eliminada duplicación entre `ToProblemDetails()` y `GetTitle()`.
- Creado método `GetErrorMapping(ErrorType)` que retorna `(int StatusCode, string Title)` como tupla.
- `ToProblemDetails()` ahora usa `GetErrorMapping()` para obtener ambos valores en una sola llamada, eliminando el switch duplicado.
- Creado `eiti.Tests/Unit/ResultExtensionsTests.cs` con 9 tests cubriendo: success genérico (OkObjectResult), success no-genérico (NoContentResult), y todos los 6 ErrorType mappings (status code + title + detail + errorCode).

### Lecciones aprendidas

- **Tuplas con nombres son ideales para eliminar switches duplicados**: Cuando dos switches mapean el mismo enum a valores diferentes, un solo switch que retorna una tupla `(int StatusCode, string Title)` es más mantenible y elimina el riesgo de que los mappings se desincronicen.
- **Errores de build de otros agentes no bloquean**: Cuando se trabaja en paralelo con otros agentes, compilar el proyecto específico con `--no-dependencies` permite verificar que tus cambios compilan sin ser bloqueado por errores en otros features.
- **Tests con `[Theory]` + `[InlineData]` cubren todos los enum values eficientemente**: Para métodos que mapean enums, un Theory con InlineData por cada valor del enum es más expresivo y mantenible que múltiples Facts individuales.

## Agent-Alpha - Extraer AuthenticationMapper - 2026-03-20

### Cambios realizados

- Creado `eiti.Application/Features/Auth/Common/AuthenticationMapper.cs` — clase estática con `MapRolesAndPermissions(User user)` que retorna `(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)`.
- Actualizado `RegisterHandler.cs`: reemplazado mapping inline de roles/permisos por llamada a `AuthenticationMapper.MapRolesAndPermissions(user)`.
- Actualizado `LoginHandler.cs`: mismo reemplazo de lógica duplicada.
- Creado `eiti.Tests/Unit/AuthenticationMapperTests.cs` con 4 tests: single role mapping, sorted permissions, seller-specific permissions, multi-role permission merging.

### Lecciones aprendidas

- **Al reemplazar un `using`, verificar si el namespace original sigue siendo referenciado**: RegisterHandler seguía necesitando `eiti.Application.Common.Authorization` para `SystemRoles`. Remover el using sin verificar causa errores de compilación.

## Agent DB — 2026-03-25

### Completed
- Added BanksManage + ChequesManage to PermissionCodes
- Created Bank, BankInstallmentPlan domain entities in eiti.Domain/Banks/
- Created Cheque, ChequeStatus domain entities in eiti.Domain/Cheques/
- Extended SalePayment + SaleCcPayment with 5 nullable card fields (CardBankId, CardCuotas, CardSurchargePct, CardSurchargeAmt, TotalCobrado)
- Created EF configs: BankConfiguration, BankInstallmentPlanConfiguration, ChequeConfiguration
- Extended SalePaymentConfiguration + SaleCcPaymentConfiguration
- Added DbSets to ApplicationDbContext
- Created IBankRepository, IChequeRepository interfaces
- Created BankRepository, ChequeRepository implementations
- Registered both repos in DI
- Generated migration: AddBanksChequesAndCardFields
- Build: SUCCESS (Domain, Application, Infrastructure — 0 errors, 0 warnings)

### Surprises / Pitfalls
- **Locked DLL build failure is not a compilation error**: The running API process (dotnet.exe PID 7872) locks DLLs in eiti.Api/bin, causing `dotnet build` for the full solution to fail with MSB3027. This does NOT mean the code has errors. Build each project with `--no-dependencies` to confirm real compilation success.
- **EF migrations must be run from eiti.Infrastructure directly** (not with --startup-project eiti.Api) when the Api DLLs are locked. The `ApplicationDbContextFactory` in Infrastructure supports design-time instantiation without the Api project.
- **Private backing field + IReadOnlyCollection navigation**: Bank uses `private readonly List<BankInstallmentPlan> _installmentPlans` with `IReadOnlyCollection<BankInstallmentPlan> InstallmentPlans` public property. BankConfiguration must call `builder.Navigation(b => b.InstallmentPlans).UsePropertyAccessMode(PropertyAccessMode.Field)` so EF uses the backing field for population — same pattern as Sale.Details/Payments.
- **HasMany FK**: When the navigation property on the owned side (`BankInstallmentPlan.BankId`) is a plain int property, use `.HasForeignKey(p => p.BankId)` instead of `.HasForeignKey("BankId")` — the typed lambda is safer and avoids shadow property confusion.

## Agent PaymentShared — 2026-03-25
### Completed
- Added banksManage + chequesManage to permission.models.ts
- Created bank.models.ts (BankInstallmentPlanResponse, BankResponse)
- Created bank.service.ts (listBanks, createBank, updateBank, upsertInstallmentPlan)
- Extended sale-payment.models.ts: ChequeFormData interface, extended SalePaymentDraftLine with card/cheque fields, extended SalePaymentRequest with card/cheque fields, updated normalizeSalePayments to pass card/cheque fields through
- Extended SalePaymentInlineComponent: added FormsModule, @Input() banks, CARD_METHOD_ID/CHECK_METHOD_ID constants, activeBanksWithPlans getter, activePlansForBank/getSurchargePct/getSurchargeAmt/updateCardBank/updateCardCuotas/toggleChequeForm/isChequeFormComplete/todayIso methods, public roundMoney wrapper, updated updatePaymentAmount to sync cheque monto and card surcharge
- Updated HTML template: card bank/cuotas selects + surcharge display, inline cheque form with all fields (adapting loop variable names payment/index from the actual template)
- Updated CSS: payment-card-fields, card-surcharge, cheque-section, cheque-form using actual CSS vars (--bg-panel, --border-2, --text-dim, --text)
- Build: SUCCESS (0 errors, warnings are pre-existing from canvg/jspdf third-party libs)
### Surprises/pitfalls
- **btn--ghost and btn--sm classes don't exist in this project**: The component uses action-chip pattern instead. Used `action-chip` class for the cheque toggle button rather than non-existent btn classes.
- **roundMoney is an imported function, not a class method**: Template calls like `roundMoney($event)` require it to be accessible as a class member. Added a public `roundMoney()` wrapper method that delegates to the imported function — this avoids breaking the TypeScript name binding from the import.
- **Template loop variables are `payment` and `index`**: Not `line`/`i` as the spec example showed. Always read the actual template before inserting new HTML.
- **ng-container placement**: The new card/cheque ng-containers must go inside the `.payment-line` div but after `.payment-line__row` — and crucially, before the closing `</div>` of `.payment-line`, not after. Check nesting carefully when inserting into deeply-nested templates.
### SIGNAL: PaymentShared READY

## Agent Backend — 2026-03-25
### Completed
- Verified all Application layer features already created by a prior agent run:
  - Banks/Queries/ListBanks: BankResponse.cs, ListBanksQuery.cs, ListBanksHandler.cs
  - Banks/Commands/CreateBank: CreateBankCommand.cs, CreateBankHandler.cs, CreateBankErrors.cs, CreateBankValidator.cs
  - Banks/Commands/UpdateBank: UpdateBankCommand.cs, UpdateBankHandler.cs, UpdateBankErrors.cs
  - Banks/Commands/UpsertInstallmentPlan: UpsertInstallmentPlanCommand.cs, UpsertInstallmentPlanHandler.cs, UpsertInstallmentPlanErrors.cs
  - Cheques/Queries/ListCheques: ListChequesQuery.cs, ListChequesHandler.cs, ChequeListItemResponse.cs
  - Cheques/Queries/GetChequeById: GetChequeByIdQuery.cs, GetChequeByIdHandler.cs, ChequeDetailResponse.cs
  - Cheques/Commands/UpdateChequeStatus: UpdateChequeStatusCommand.cs, UpdateChequeStatusHandler.cs, UpdateChequeStatusErrors.cs
- Verified AddCcPaymentGroupCommand.cs already has ChequeData record and updated CcPaymentMethodLine with CardBankId/CardCuotas/Cheque
- Verified AddCcPaymentGroupHandler.cs already has card surcharge + cheque creation logic
- Verified CreateSaleCommand.cs already has CreateSalePaymentChequeData and updated CreateSalePaymentItemRequest
- Verified CreateSaleHandler.cs already has card + cheque logic with IBankRepository/IChequeRepository injected
- Verified SaleCcPayment.SetCardData and SalePayment.SetCardData already exist in domain
- Verified ListCcPaymentsItemResponse already has card fields; ListCcPaymentsHandler already fetches bank names
- Verified ISaleRepository already has GetSaleIdsByCcPaymentIdsAsync; SaleRepository already implements it using SaleCcPaymentId value objects
- Verified BanksController.cs and ChequesController.cs already created with correct routes/actions
### Build result
SUCCESS — Domain: 0 errors, Application: 0 errors (with dependencies), Infrastructure: 0 errors. eiti.Api build shows only MSB3027 file-lock errors (running process PID 7872 holds DLLs) — NOT compilation errors.
### Pitfalls
- **dotnet build --no-dependencies causes false error CS1061 on SetCardData**: When building Application with --no-dependencies, it uses stale cached Domain DLLs that pre-date the SetCardData methods. Always build WITH dependencies (`dotnet build eiti.Application/eiti.Application.csproj`) to get accurate results. The --no-dependencies flag is only safe if you have freshly built all dependencies first.
- **All work was done by prior agent runs**: The entire Application layer, API controllers, and Infrastructure changes were already fully implemented before this agent ran. The main task was verification and build confirmation.
### SIGNAL: Backend READY

## Agent Frontend-CC — 2026-03-25
### Completed
- Added DETALLE button and read-only sale detail modal to clients-cc
- Updated sales-cc-payments to load banks and pass card/cheque data in request
- Build: SUCCESS (0 errors, warnings are pre-existing from canvg/jspdf third-party libs)
### SIGNAL: Frontend-CC READY

## Frontend fixes — 2026-03-27

### Fix: card surcharge structural refactor (recalculateSurcharge)

**Root cause (structural):** `cardSurchargePct` was cached in state and each mutation point (`updateCardCuotas`, `updatePaymentAmount`) read from a DIFFERENT place:
- `updateCardCuotas` called `getSurchargePct()` (fresh lookup) but only when cuotas changed
- `updatePaymentAmount` read `this.state.payments[index].cardSurchargePct` (cached value)

If `cardSurchargePct` was ever stale/null (e.g., bank was changed and re-selected), `updatePaymentAmount` would silently skip the recalculation. The state was fragile because it depended on all three mutation points staying in sync.

**Fix:** Extracted `recalculateSurcharge(index)` as a single private method that:
1. Derives `surchargePct` fresh from authoritative sources (`getSurchargePct(bankId, cuotas)`)
2. Sets both `cardSurchargePct` and `cardSurchargeAmt` atomically
3. Is called from ALL three mutation points: `updateCardBank`, `updateCardCuotas`, `updatePaymentAmount`

**Pattern to remember:** When multiple mutation handlers need to keep derived state consistent, extract a single `recalculate*()` method and call it from all of them. Never let handlers read each other's cached state.

**Console.logs added** to trace: which handler fired, what value monto had, what surchargePct was found, what the formula result was. Look for `[PaymentInline]` prefix in browser console to diagnose remaining issues.

---

## Frontend fixes — 2026-03-26

### Fix 1: Card surcharge showing $0.00

**Root cause:** Two compounding issues:
1. Bank/cuotas `<option>` elements used `[value]="bank.id"` / `[value]="plan.cuotas"` — this serializes the number to a string in the DOM. The parent `<select>` used `[ngModel]="payment.cardBankId"` (a `number | null`), so Angular's equality check (`number === string`) always failed: the selected option never matched, `ngModelChange` fired with a string instead of a number, and `getSurchargePct()` received the wrong type.
2. Additionally, `[ngModel]="payment.cardBankId || ''"` — using `|| ''` as fallback compounded the mismatch. The placeholder `<option value="">` would then match on reset but no real bank would ever match.

**Fix:** Use `[ngValue]="bank.id"` / `[ngValue]="plan.cuotas"` on all `<option>` elements that bind numeric values. Use `[ngValue]="null"` for the disabled placeholder. Change `[ngModel]` fallback from `|| ''` to `?? null` so it stays a number-or-null. Update `updateCardBank` / `updateCardCuotas` signatures from `value: string` to `value: number | null` to avoid re-parsing.

**Also fixed:** Monto input switched from `[value]="payment.amount" (input)="handler($any($event.target).value)"` to `[ngModel]="payment.amount" (ngModelChange)="handler($event)" [ngModelOptions]="{standalone:true}"`. The `[value]` + `(input)` pattern causes Angular to re-evaluate `[value]` on every CD cycle and potentially reset the DOM input; `ngModelChange` also delivers a typed number instead of a raw string.

---

### Fix 2: sales-cc sections visually cramped

**Root cause:** Two issues:
1. `.grid` selector appeared in three separate rule blocks — display+gap combined with `.stack`, then margin-top alone, then grid-template-columns alone. While correct, the fragmentation made it unclear which declarations were active and made future edits error-prone.
2. `.block` had `margin-top` but no visual separator, so adjacent sections blended together without obvious boundaries.

**Fix:** Consolidated `.grid` into a single rule block. Added `border-top` + `padding-top` to `.block` to create clear visual section dividers.

---

### Fix 3: Bank/cuotas selects stacking vertically

**Root cause:** `.payment-card-fields { flex-direction: column }` — this was never previously investigated. The layout intent was side-by-side but the container was explicitly set to column.

**Fix:** Changed to `flex-direction: row; flex-wrap: wrap; align-items: flex-end` with `.payment-card-fields .field { flex: 1 1 160px }` so bank and cuotas sit side-by-side (~50% each). `.card-surcharge` gets `flex: 0 0 100%` so it spans the full row below.

---

### General pattern: always check the CSS container first

When a layout doesn't match expectations (e.g., items stacking instead of side-by-side), the first thing to check is the **container's `flex-direction`** — not the children. The root cause is almost always at the container level.

## Agent Frontend-ABM — 2026-03-25
### Completed
- Created BanksComponent (ABM with installment plan management, expandable rows, per-plan save)
- Created ChequesComponent (list + filters + detail modal + state transitions)
- Created cheque.models.ts + cheque.service.ts
- Added /banks + /cheques routes to app.routes.ts (near /users)
- Added Banks + Cheques sidebar links to navbar (guarded by banksManage/chequesManage permissions)
- Updated sales-page.component.ts + sales-full.component.ts to inject BankService, load banks on init, pass [banks] to all SalePaymentInlineComponent instances
- Added CC popup to cash.component.ts (Feature 5): openCcPopup/closeCcPopup/ccPopupSale getter, paymentMethodLabel helper, referenceId field added to getDisplayRows return type, click handler on CuentaCorrienteIncome rows, CC popup modal with header/totals/payment history
- Build: SUCCESS (0 errors, warnings are pre-existing from canvg/jspdf third-party libs)
### Surprises/pitfalls
- **FormControl type in template**: `$any(planForms.get(id)?.get(key))` pattern from spec causes type issues in strict mode. Instead, added a `getFormControl(bankId, controlName): FormControl` helper method on the component — cleaner and fully typed.
- **getDisplayRows return type**: The spec required referenceId for the CC popup click handler. Added `referenceId: string | null` to the return type so the template can access it.
- **Inline template import()**: Used a dynamic import type annotation `import('...').CcPaymentResponse[]` for the ccPopupPayments field to avoid adding a top-level import for a type already implicitly available — alternatively could have relied on the existing SaleByIdResponse import which already imports CcPaymentResponse.
- **No btn--sm in this project**: Per PaymentShared agent lessons, btn--sm wasn't in the global styles. Added it in the component-level CSS for Banks and Cheques.
