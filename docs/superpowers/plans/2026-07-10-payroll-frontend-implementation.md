# Payroll (pago de salarios) — Frontend Implementation Plan

> **For agentic workers:** This plan is written for execution by a different agent (not the one that wrote it) in a separate session, following superpowers:executing-plans conventions — batch execution with checkpoints, or task-by-task if the executing agent supports subagent-driven-development. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Angular frontend for the payroll module: employee salary/periodicity config, a deduction-concepts catalog ABM, advances (create/cancel/list), and liquidations (batch generation, pay, cancel, list with detail + PDF receipt).

**Architecture:** New standalone-component feature at `src/app/features/payroll/`, lazy-loaded, with sub-routes for each of the 4 sub-screens (Empleados / Descuentos / Adelantos / Liquidaciones), following the exact patterns already used by `src/app/features/sales/sales-full.component.ts` (parent shell + sub-routes) and `src/app/features/*/banks.component.ts`-style ABM screens.

**Tech Stack:** Angular 16, standalone components, RxJS 7, TypeScript strict, plain CSS with custom properties, Reactive Forms, jsPDF for the receipt (same as `audit.component.ts`/existing sale PDFs).

**Backend status:** The backend is fully implemented, tested (127/127 backend tests passing), and committed on `develop` at commit `5cad474` in the `eiti` repo (`C:/Eiti/eiti`). All contracts below were read directly from the actual C# source, not assumed — they are final.

## Global Constraints

- Standalone components only, `OnPush` change detection, lazy-loaded routes (`loadComponent`).
- All HTTP calls go through `core/services/*.service.ts`, `providedIn: 'root'`, typed `HttpClient<T>`, URLs built from `environment.apiUrl` — never hardcoded.
- Reactive Forms only (`FormBuilder`/`FormGroup`/`Validators`), no template-driven forms.
- All models/interfaces live in `core/models/payroll.models.ts` — no inline types in services or components.
- New permission codes (already defined and assigned to Owner/Admin on the backend): `payroll.manage`, `payroll.liquidations.generate`, `payroll.liquidations.pay`, `payroll.advances.manage`. Add these to the frontend's `PermissionCodes` map in `core/models/permission.models.ts` (read that file first — find the existing map, e.g. `banksManage: 'banks.manage'`, and add the payroll ones in the same style) and to `PermissionCatalog` array if one exists in that file.
- CSS: use existing custom properties (`--bg-panel`, `--border-2`, `--text-dim`, `--text`, etc. — read `src/styles.css` or any existing feature's `.css` file for the exact variable names in use, they must match, do not invent new ones).
- Errors surfaced via the existing `ToastService`, never `console.error` alone.
- Money formatting: use `| currency:'USD':'symbol':'1.2-2'` pipe, matching the pattern already used in `sales-page.component.html` (search that file for `currency:'USD'` to see exact usage).

---

## Contracts (read directly from the actual backend source — final, do not re-derive)

### Base URL pattern
All payroll endpoints require `Authorization: Bearer <token>` (existing `AuthInterceptor` already handles this for any `HttpClient` call — no special wiring needed). Base path: `${environment.apiUrl}/api/...`.

### 1. Deduction concepts — `api/payroll-deduction-concepts`

| Verb | Route | Body | Response |
|---|---|---|---|
| GET | `?activeOnly={bool}` | — | `DeductionConceptResponse[]` |
| POST | `` | `{ name: string, percentage: number }` | `DeductionConceptResponse` |
| PUT | `{id}` | `{ name: string, percentage: number }` | `DeductionConceptResponse` |
| PUT | `{id}/active` | `{ isActive: boolean }` | `DeductionConceptResponse` |

`DeductionConceptResponse = { id: string; name: string; percentage: number; isActive: boolean }`

### 2. Employee payroll config — `api/employees/{id}/payroll-config`

| Verb | Route | Body | Response |
|---|---|---|---|
| PUT | `{id}/payroll-config` | `{ baseSalary: number \| null, payrollPeriodicity: number \| null }` | `{ employeeId: string; baseSalary: number \| null; payrollPeriodicity: number \| null }` |

`PayrollPeriodicity`: `1 = Monthly`, `2 = Biweekly`.

The **existing** `GET api/employees` (already implemented, do not touch) now also returns `baseSalary: number | null` and `payrollPeriodicity: number | null` on every employee — the existing `EmployeeResponse` TS interface in `core/models/employee.models.ts` (find its exact name by reading that file) needs these two fields added so the payroll "Empleados" screen can prefill the form with current values. This is the ONLY change to an existing model file in this whole plan — everything else is additive/new files.

### 3. Advances — `api/payroll-advances`

| Verb | Route | Body/Query | Response |
|---|---|---|---|
| GET | `?employeeId={guid?}&status={int?}` | — | `PayrollAdvanceResponse[]` |
| POST | `` | `{ employeeId: string, amount: number, date: string, notes: string \| null, paymentMethod: number, cashSessionId: string \| null }` | `PayrollAdvanceResponse` |
| POST | `{id}/cancel` | — | `PayrollAdvanceResponse` |

`PayrollAdvanceResponse = { id: string; employeeId: string; amount: number; date: string; notes: string | null; status: number; appliedToLiquidationId: string | null; cashSessionId: string | null }`

`status`: `1 = Pending`, `2 = Applied`, `3 = Cancelled`.
`paymentMethod` (request only, not on the response — see note below): `1 = Cash`, `2 = Transfer`, `3 = Other`. **`cashSessionId` is required when `paymentMethod === 1` (Cash)**, otherwise must be `null`. The response's `cashSessionId` reflects whether it was cash-paid (non-null) or not (null) — there's no separate `paymentMethod` field on the response, infer "was cash" from `cashSessionId !== null`.

### 4. Liquidations — `api/payroll-liquidations`

| Verb | Route | Body/Query | Response |
|---|---|---|---|
| GET | `?employeeId={guid?}&periodLabel={string?}&status={int?}&page={int}&pageSize={int}` | — | `ListLiquidationsResponse` |
| GET | `{id}` | — | `PayrollLiquidationResponse` |
| POST | `generate` | `{ periodicity: number, periodLabel: string, periodStart: string, periodEnd: string }` | `GeneratePayrollPeriodResponse` |
| POST | `{id}/pay` | `{ paymentMethod: number, cashSessionId: string \| null }` | `PayrollLiquidationResponse` |
| POST | `{id}/cancel` | — | `PayrollLiquidationResponse` |

```ts
interface PayrollLiquidationLineResponse { label: string; amount: number; }

interface PayrollLiquidationResponse {
  id: string;
  employeeId: string;
  periodLabel: string;       // e.g. "2026-07"
  grossAmount: number;
  netAmount: number;
  status: number;            // 1 = Pending, 2 = Paid, 3 = Cancelled
  paymentMethod: number | null;  // 1 = Cash, 2 = Transfer, 3 = Other
  paidAt: string | null;
  deductionLines: PayrollLiquidationLineResponse[];
  advanceLines: PayrollLiquidationLineResponse[];   // each line's label is always literally "Adelanto"
}

interface ListLiquidationsResponse {
  items: PayrollLiquidationResponse[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

interface PayrollLiquidationSummary { id: string; employeeId: string; employeeName: string; netAmount: number; }
interface GeneratePayrollPeriodSkippedItem { employeeId: string; employeeName: string; reason: string; }
interface GeneratePayrollPeriodResponse {
  generatedCount: number;
  generated: PayrollLiquidationSummary[];
  skipped: GeneratePayrollPeriodSkippedItem[];
}
```

`PayrollLiquidationResponse` does NOT include the employee's name or code — only `employeeId` (a GUID). The frontend must resolve employee names itself from the already-loaded employee list (same pattern as `audit.component.ts`'s `idNameMap`, or simpler: build a `Map<string, string>` from the employee list once and look up by id when rendering).

**Important error-response note:** all endpoints return the project's standard `ProblemDetails`-shaped error body on failure (same as every other feature in this app) — reuse the existing HTTP error handling pattern already present in any current `*.service.ts` (e.g. `bank.service.ts`), do not invent a new one.

---

## Task 1: Models + services

**Files:**
- Create: `src/app/core/models/payroll.models.ts`
- Modify: `src/app/core/models/employee.models.ts` (add `baseSalary`/`payrollPeriodicity` to the existing employee response interface — read the file first to find its exact name and current field list)
- Modify: `src/app/core/models/permission.models.ts` (add the 4 new permission codes to the existing map/catalog, matching its current style exactly)
- Create: `src/app/core/services/payroll-deduction-concept.service.ts`
- Create: `src/app/core/services/payroll-advance.service.ts`
- Create: `src/app/core/services/payroll-liquidation.service.ts`
- Create: `src/app/core/services/employee-payroll-config.service.ts`

**Interfaces produced (exact, in `payroll.models.ts`):**

```ts
export type PayrollPeriodicity = 1 | 2; // 1 Monthly, 2 Biweekly
export const PAYROLL_PERIODICITIES: { value: PayrollPeriodicity; label: string }[] = [
  { value: 1, label: 'Mensual' },
  { value: 2, label: 'Quincenal' },
];

export type PayrollPaymentMethod = 1 | 2 | 3; // 1 Cash, 2 Transfer, 3 Other
export const PAYROLL_PAYMENT_METHODS: { value: PayrollPaymentMethod; label: string }[] = [
  { value: 1, label: 'Efectivo' },
  { value: 2, label: 'Transferencia' },
  { value: 3, label: 'Otro' },
];

export interface DeductionConceptResponse {
  id: string;
  name: string;
  percentage: number;
  isActive: boolean;
}
export interface CreateDeductionConceptRequest { name: string; percentage: number; }
export interface UpdateDeductionConceptRequest { name: string; percentage: number; }

export interface PayrollAdvanceResponse {
  id: string;
  employeeId: string;
  amount: number;
  date: string;
  notes: string | null;
  status: number; // 1 Pending, 2 Applied, 3 Cancelled
  appliedToLiquidationId: string | null;
  cashSessionId: string | null;
}
export interface CreatePayrollAdvanceRequest {
  employeeId: string;
  amount: number;
  date: string;
  notes: string | null;
  paymentMethod: PayrollPaymentMethod;
  cashSessionId: string | null;
}

export interface PayrollLiquidationLineResponse { label: string; amount: number; }
export interface PayrollLiquidationResponse {
  id: string;
  employeeId: string;
  periodLabel: string;
  grossAmount: number;
  netAmount: number;
  status: number; // 1 Pending, 2 Paid, 3 Cancelled
  paymentMethod: number | null;
  paidAt: string | null;
  deductionLines: PayrollLiquidationLineResponse[];
  advanceLines: PayrollLiquidationLineResponse[];
}
export interface ListLiquidationsResponse {
  items: PayrollLiquidationResponse[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
export interface PayrollLiquidationSummary { id: string; employeeId: string; employeeName: string; netAmount: number; }
export interface GeneratePayrollPeriodSkippedItem { employeeId: string; employeeName: string; reason: string; }
export interface GeneratePayrollPeriodResponse {
  generatedCount: number;
  generated: PayrollLiquidationSummary[];
  skipped: GeneratePayrollPeriodSkippedItem[];
}
export interface GeneratePayrollPeriodRequest {
  periodicity: PayrollPeriodicity;
  periodLabel: string;
  periodStart: string;
  periodEnd: string;
}
export interface PayLiquidationRequest { paymentMethod: PayrollPaymentMethod; cashSessionId: string | null; }

export interface SetEmployeePayrollConfigRequest { baseSalary: number | null; payrollPeriodicity: PayrollPeriodicity | null; }
export interface SetEmployeePayrollConfigResponse { employeeId: string; baseSalary: number | null; payrollPeriodicity: number | null; }
```

**Services** — each follows the exact structure of `src/app/core/services/bank.service.ts` (`providedIn: 'root'`, `private readonly baseUrl = \`${environment.apiUrl}/api/payroll-...\`;`, typed `HttpClient` calls). Read that file first, then produce:

- `PayrollDeductionConceptService`: `list(activeOnly: boolean): Observable<DeductionConceptResponse[]>`, `create(req: CreateDeductionConceptRequest): Observable<DeductionConceptResponse>`, `update(id: string, req: UpdateDeductionConceptRequest): Observable<DeductionConceptResponse>`, `setActive(id: string, isActive: boolean): Observable<DeductionConceptResponse>`.
- `PayrollAdvanceService`: `list(employeeId?: string, status?: number): Observable<PayrollAdvanceResponse[]>`, `create(req: CreatePayrollAdvanceRequest): Observable<PayrollAdvanceResponse>`, `cancel(id: string): Observable<PayrollAdvanceResponse>`.
- `PayrollLiquidationService`: `list(filters: { employeeId?: string; periodLabel?: string; status?: number; page: number; pageSize: number }): Observable<ListLiquidationsResponse>`, `getById(id: string): Observable<PayrollLiquidationResponse>`, `generate(req: GeneratePayrollPeriodRequest): Observable<GeneratePayrollPeriodResponse>`, `pay(id: string, req: PayLiquidationRequest): Observable<PayrollLiquidationResponse>`, `cancel(id: string): Observable<PayrollLiquidationResponse>`.
- `EmployeePayrollConfigService`: `set(employeeId: string, req: SetEmployeePayrollConfigRequest): Observable<SetEmployeePayrollConfigResponse>` — `PUT ${environment.apiUrl}/api/employees/${employeeId}/payroll-config`.

- [ ] Read `bank.service.ts`, `employee.models.ts`, `permission.models.ts` for exact current patterns/field names.
- [ ] Create `payroll.models.ts` with the interfaces above verbatim.
- [ ] Add `baseSalary: number | null; payrollPeriodicity: number | null;` to the existing employee response interface in `employee.models.ts`.
- [ ] Add the 4 payroll permission codes to `permission.models.ts`'s existing map/catalog.
- [ ] Create the 4 services above.
- [ ] Run `cd C:/EiTeFront/eiti-front && ng build --configuration development` — expect 0 errors. Fix any type errors before proceeding (services/models with no consumers yet should compile cleanly on their own).
- [ ] Commit: `feat(payroll): modelos y servicios de payroll`

---

## Task 2: Deduction concepts ABM

**Files:**
- Create: `src/app/features/payroll/deduction-concepts/deduction-concepts.component.ts`
- Create: `src/app/features/payroll/deduction-concepts/deduction-concepts.component.html`
- Create: `src/app/features/payroll/deduction-concepts/deduction-concepts.component.css`

**Pattern to mirror:** find the existing Banks ABM component (search `src/app/features/` for a component that lists/creates/edits banks — it manages a simple named-catalog-with-percentage-like entity, e.g. installment plans with a surcharge %). Copy its structure: a list/table, an inline or modal create form, edit-in-place or edit-modal, and an active/inactive toggle. Reuse `SearchableSelectComponent` if the reference component uses it for anything comparable, otherwise plain `<table>`.

**Behavior:**
- On init, load `list(false)` (all, not just active) so the ABM can show and toggle inactive ones.
- Form fields: `name` (required, max 150), `percentage` (required, `Validators.min(0)`, `Validators.max(100)`).
- Create → `service.create(...)`, on success prepend/refresh the list, `ToastService` success message, reset form.
- Edit → prefill form, on submit `service.update(id, ...)`.
- Toggle active/inactive → `service.setActive(id, !current.isActive)`, update the row in place on success.
- All errors → `ToastService.error(...)`, no silent failures, no `console.error`-only handling.
- Guard the whole component behind `payroll.manage` permission (check how an existing component conditionally hides/disables actions based on `AuthService.hasPermission(...)` — mirror that, e.g. hide the create/edit/toggle controls if the user lacks the permission, but the route itself is already guarded at the route level in Task 6).

- [ ] Implement per the pattern above.
- [ ] `ng build --configuration development` — 0 errors.
- [ ] Manually verify in a running dev server (`ng serve`) if possible: create a concept, edit it, deactivate it. If a dev server isn't available in this environment, at minimum confirm the build is clean and the template bindings match the component's public members (no `strictTemplates` errors).
- [ ] Commit: `feat(payroll): ABM de conceptos de descuento`

---

## Task 3: Employee salary config (extend existing Employees grid)

**Files:**
- Modify: the existing Employees list component (find it — likely `src/app/features/employees/` or similar; search for where `EmployeeResponse`/employee list is rendered in a table).

**Behavior:**
- Add two columns to the existing employee table: "Sueldo base" and "Periodicidad" (read-only display: format `baseSalary` with the currency pipe if non-null, else show "—"; `payrollPeriodicity` mapped via `PAYROLL_PERIODICITIES` to its label, else "—").
- Add an action (icon button or existing row-action menu, matching however this table already exposes per-row actions) — "Configurar sueldo" — that opens a small modal/inline form with two fields: `baseSalary` (number input, optional) and `payrollPeriodicity` (select from `PAYROLL_PERIODICITIES`, only enabled/required when `baseSalary` has a value — mirror the backend rule: periodicity is required only if a base salary is set, clearing baseSalary should clear periodicity too, matching `Employee.SetPayrollConfig`'s domain rule).
- Prefill the modal from the employee row's current `baseSalary`/`payrollPeriodicity` (now available thanks to Task 1's model change).
- On submit: `EmployeePayrollConfigService.set(employee.id, { baseSalary, payrollPeriodicity })`, on success update the row's displayed values from the response, `ToastService` success, close modal. On failure, `ToastService.error(...)`, keep modal open.
- Gate this action behind `payroll.manage` permission, same pattern as Task 2.

**Do not** create a new "Empleados" screen under `/payroll` — this constraint from the original design spec is superseded by this plan: extending the existing Employees screen is simpler and avoids a duplicate employee list UI. If the existing Employees component is large and this addition would make it unwieldy, extract the two new columns + modal into a small child component instead of inlining more logic into an already-big file, but keep it wired from the existing screen — do not create a parallel employee list.

- [ ] Locate the existing Employees component and read it in full before editing.
- [ ] Add the two columns + action + modal per the behavior above.
- [ ] `ng build --configuration development` — 0 errors.
- [ ] Commit: `feat(payroll): configurar sueldo base y periodicidad desde Empleados`

---

## Task 4: Advances screen

**Files:**
- Create: `src/app/features/payroll/advances/advances.component.ts`
- Create: `src/app/features/payroll/advances/advances.component.html`
- Create: `src/app/features/payroll/advances/advances.component.css`

**Behavior:**
- On init: load the employee list (for the employee picker and to resolve `employeeId → name` in the table) and `PayrollAdvanceService.list()` (no filters = all).
- Filters: employee (searchable select, reuse `SearchableSelectComponent`), status (`Pendiente` 1 / `Aplicado` 2 / `Cancelado` 3 / todos).
- Table columns: Empleado (resolved name), Monto, Fecha, Estado (chip, color-coded — reuse whatever chip/badge CSS class pattern an existing status column uses, e.g. `sales-page.component.html`'s `chip--status`), Notas, Acciones.
- "Nuevo adelanto" button opens a form: empleado (required), monto (required, `Validators.min(0.01)`), fecha (required, default today), notas (optional), medio de pago (`PAYROLL_PAYMENT_METHODS`). When medio de pago = Efectivo (1): show a caja/cash-session picker — **reuse whatever existing UI pattern this app already uses to pick an open cash session** (search for `CashSessionId`/`cashDrawerId` selection in an existing form, e.g. wherever `CreateCashWithdrawal` or a purchase-payment form lets the user pick a drawer/session — mirror that exactly, since it must resolve to a real open `CashSessionId` the current user can access). When medio de pago ≠ Efectivo, this field is hidden and `cashSessionId` is sent as `null`.
- On create: `PayrollAdvanceService.create(...)`. On the backend's `Payroll.Advances.Create.CashSessionRequired`/`CashSessionNotFound` errors (surfaced via the standard error body), show the error message via `ToastService`.
- "Cancelar" action per row, only enabled when `status === 1` (Pending) — confirm via a simple confirm dialog (reuse whatever confirm-modal pattern exists elsewhere, e.g. sale cancellation), then `PayrollAdvanceService.cancel(id)`, update the row's status in place on success.
- Gate creation/cancellation behind `payroll.advances.manage` permission.

- [ ] Implement per the behavior above, reusing the cash-session-picker pattern found in an existing form.
- [ ] `ng build --configuration development` — 0 errors.
- [ ] Commit: `feat(payroll): pantalla de adelantos de sueldo`

---

## Task 5: Liquidations screen (generate / pay / cancel / detail)

**Files:**
- Create: `src/app/features/payroll/liquidations/liquidations.component.ts`
- Create: `src/app/features/payroll/liquidations/liquidations.component.html`
- Create: `src/app/features/payroll/liquidations/liquidations.component.css`

**Behavior:**
- Filters: período (text input matching `PeriodLabel` format, e.g. "2026-07" — or a month picker that formats to that string), empleado, estado. Paginated table (reuse the pagination UI pattern from `sales-page.component`'s `salesPageSize`/pager, or `audit.component`'s pager — pick whichever is closer in this app's convention) driven by `page`/`pageSize` against `ListLiquidationsResponse.totalPages`.
- "Generar liquidaciones" button opens a small form: periodicidad (`PAYROLL_PERIODICITIES`), período (label + start/end dates — for Monthly, default start/end to the first/last calendar day of the selected month; for Biweekly, let the user pick both dates directly since there's no single fixed convention). On submit: `PayrollLiquidationService.generate(...)`, then show the result summary in a modal/panel: "`{generatedCount}` liquidaciones generadas" plus a list of `skipped` items with their `reason` (this is the batch's whole point — the user must see who got skipped and why, don't just toast a count and discard the detail). Refresh the table after.
- Table columns: Empleado (resolved name), Período, Bruto, Neto, Estado (chip), Fecha de pago, Acciones.
- Row actions:
  - "Pagar" (only when `status === 1` Pending): opens a small form — medio de pago (`PAYROLL_PAYMENT_METHODS`), and the same cash-session picker as Task 4 when medio de pago = Efectivo. On submit: `PayrollLiquidationService.pay(id, {...})`, update the row in place.
  - "Cancelar" (when `status !== 3`): confirm dialog, then `PayrollLiquidationService.cancel(id)`. Surface the backend's `Conflict` error (e.g. "closed cash session") via `ToastService` if it fails — do not swallow it.
  - "Ver detalle / recibo": expand the row (mirror `audit.component.ts`'s `expandedId`/toggle-detail pattern) showing `deductionLines`/`advanceLines`/`grossAmount`/`netAmount`, plus a "Descargar recibo" button.
- Gate "Generar liquidaciones" behind `payroll.liquidations.generate`, "Pagar"/"Cancelar" behind `payroll.liquidations.pay`.

- [ ] Implement per the behavior above.
- [ ] `ng build --configuration development` — 0 errors.
- [ ] Commit: `feat(payroll): pantalla de liquidaciones (generar, pagar, cancelar, detalle)`

---

## Task 6: PDF receipt

**Files:**
- Modify: `src/app/features/payroll/liquidations/liquidations.component.ts` (add an `exportReceiptPdf(liquidation: PayrollLiquidationResponse): void` method)

**Pattern to mirror:** `src/app/features/audit/audit.component.ts`'s `exportPdf()` method and its use of `PdfBrandingService`/`PdfLayoutService` — read that method in full before writing this one, reuse the same header/branding/footer helpers, do not reinvent PDF layout primitives.

**Content:** company branding header (via `PdfBrandingService`), employee name (resolved from the id, same map used elsewhere in this component), período (`periodLabel`), fecha de pago (`paidAt`), medio de pago (mapped via `PAYROLL_PAYMENT_METHODS`), a table of `deductionLines` (concept + amount), a table of `advanceLines` (amount each — label is always "Adelanto"), `grossAmount`, and `netAmount` as the final total. File name: `recibo_sueldo_{employeeName}_{periodLabel}.pdf`.

- [ ] Implement `exportReceiptPdf`, wire the button added in Task 5.
- [ ] `ng build --configuration development` — 0 errors.
- [ ] Commit: `feat(payroll): recibo de sueldo en PDF`

---

## Task 7: Routes + navbar

**Files:**
- Modify: `src/app/app.routes.ts` (add the 3 lazy routes below)
- Modify: the navbar/sidebar component (find where existing feature links live, e.g. near an "Empleados" or "Reportes" group — search for how `banksManage`-gated links are added)

**Routes to add** (`loadComponent`, matching this app's existing lazy-route style exactly — copy the shape of an existing route entry):

```ts
{ path: 'payroll/deduction-concepts', loadComponent: () => import('./features/payroll/deduction-concepts/deduction-concepts.component').then(m => m.DeductionConceptsComponent), data: { permission: PermissionCodes.payrollManage } },
{ path: 'payroll/advances', loadComponent: () => import('./features/payroll/advances/advances.component').then(m => m.AdvancesComponent), data: { permission: PermissionCodes.payrollAdvancesManage } },
{ path: 'payroll/liquidations', loadComponent: () => import('./features/payroll/liquidations/liquidations.component').then(m => m.LiquidationsComponent), data: { permission: PermissionCodes.payrollManage } },
```

(Adjust the exact `PermissionCodes` member names to whatever Task 1 actually named them in `permission.models.ts` — this snippet assumes camelCase keys matching the existing map's convention; verify against the real file.)

**Navbar:** add a "Sueldos" group/section with three links (Conceptos de descuento, Adelantos, Liquidaciones) guarded by the same permissions, near the existing Empleados section — mirror exactly how the Banks/Cheques links were added (per this repo's own history, they were added "bajo el grupo de Empleados" for Banks — do the same grouping logic here, or place under a sensible existing group if the navbar structure has changed since).

- [ ] Add the 3 routes.
- [ ] Add the navbar links.
- [ ] `ng build --configuration development` — 0 errors.
- [ ] Manually click through all 3 new nav links in a running `ng serve` if available, confirm no 404/blank screens.
- [ ] Commit: `feat(payroll): rutas y navegacion del modulo de sueldos`

---

## Task 8: End-to-end smoke pass

Not a code task — a manual verification pass using the actual running backend (`eiti.Api`, already fully working, `develop` branch) and frontend together.

- [ ] Start the backend locally (or point `environment.apiUrl` at the deployed API) and the frontend (`ng serve`).
- [ ] Log in as a user with Owner or Admin role (payroll permissions are only assigned to those two roles).
- [ ] Create a deduction concept (e.g. "Jubilación", 11%).
- [ ] Set a base salary + monthly periodicity on an existing employee.
- [ ] Create a cash advance for that employee, confirm it shows Pending.
- [ ] Generate liquidations for the current month — confirm the employee appears in "generated" with a net amount that subtracts both the deduction % and the advance.
- [ ] Pay the liquidation by transfer — confirm status flips to Paid, no cash movement created.
- [ ] Cancel that liquidation — confirm the advance reverts to Pending and the liquidation shows Cancelled.
- [ ] Repeat pay/cancel once more using Efectivo with a real open cash session — confirm the cash session's movement history shows the payroll expense, and that cancelling it reverses the movement.
- [ ] Download a PDF receipt for a paid liquidation, confirm it opens and shows correct totals.
- [ ] Report any mismatch between actual behavior and this plan's assumptions back to the plan owner — do not silently "fix" a contract mismatch by guessing at a different shape; the contracts section above was read from source and should be authoritative, so a mismatch here likely means the running API is stale or a different branch/commit.
