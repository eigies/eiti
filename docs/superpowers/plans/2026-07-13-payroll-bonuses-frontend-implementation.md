# Payroll Bonuses Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose the already-shipped backend "Payroll Bonuses" feature (presentismo, bonificación por venta — per-employee salary bonuses, fixed amount or percentage of base salary) in the Angular app: an ABM for bonus concepts, a create/list/cancel screen for per-employee bonus assignments, and a "Bonificaciones" section wired into the existing liquidation detail view and PDF receipt.

**Architecture:** No new visual decisions — this is a straight extension of the existing, already-designed Payroll module in `src/app/features/payroll/`. Two new screens are near-identical copies of two existing sibling screens (`deduction-concepts` → bonus-concept ABM; `advances` → per-employee bonus create/list, minus the cash-session integration advances has, since bonuses don't move cash directly). No new CSS patterns, no new shared components — reuses `SearchableSelectComponent`, `ConfirmationService`, `ToastService`, the existing `.page/.hero/.panel/.field/.control/.btn/.badge` system, and the `PdfBrandingService`/`PdfLayoutService` pair already used by the liquidation receipt PDF.

**Tech Stack:** Angular 16 standalone components, Reactive Forms, RxJS, jsPDF (existing `PdfBrandingService`/`PdfLayoutService`).

## Global Constraints

- Backend API base URL: `environment.apiUrl`. New endpoints (already shipped, backend `develop` branch): `GET/POST /payroll-bonus-concepts`, `PUT /payroll-bonus-concepts/{id}`, `PUT /payroll-bonus-concepts/{id}/active`, `GET/POST /payroll-bonuses`, `POST /payroll-bonuses/{id}/cancel`.
- Both new screens are gated by `PermissionCodes.payrollManage` (`'payroll.manage'`) — the backend commands for bonus concepts AND bonus assignments both reuse this single permission (confirmed: no new permission code was added on the backend). Do not gate the "Bonuses" screen by `payrollAdvancesManage` — that's a different, unrelated permission that happens to gate the sibling Advances screen.
- `AmountType`: `1 = FixedAmount`, `2 = Percentage` (matches backend `PayrollBonusAmountType` enum exactly).
- `Status`: `1 = Pending`, `2 = Applied`, `3 = Cancelled` (matches backend `PayrollBonusStatus`, same values/semantics as the existing `PayrollAdvanceStatus` already used in `AdvancesComponent`).
- No `any` types. All new interfaces exported from `payroll.models.ts`. OnPush change detection on every new component (match existing siblings).
- After every task, run `cd C:/EiTeFront/eiti-front && ng build --configuration development` and confirm 0 errors before moving on — this is not optional per this project's frontend verification rule.
- Frontend work happens on `develop` (already checked out; `main` must stay untouched until an explicit later merge/deploy step).

---

### Task 1: Models and HTTP services

**Files:**
- Modify: `src/app/core/models/payroll.models.ts`
- Create: `src/app/core/services/payroll-bonus-concept.service.ts`
- Create: `src/app/core/services/payroll-bonus.service.ts`

**Interfaces:**
- Produces: `BonusConceptResponse`, `CreateBonusConceptRequest`, `UpdateBonusConceptRequest`, `PayrollBonusAmountType`, `PAYROLL_BONUS_AMOUNT_TYPES`, `PayrollBonusResponse`, `CreatePayrollBonusRequest`, and the extended `PayrollLiquidationResponse` (gains `bonusLines`) — all consumed by Tasks 2-4.

- [ ] **Step 1: Extend `payroll.models.ts`**

Add these new types (append after the existing `PayrollAdvanceResponse`/`CreatePayrollAdvanceRequest` block, before the `PayrollLiquidationLineResponse` block):

```typescript
export interface BonusConceptResponse {
  id: string;
  name: string;
  isActive: boolean;
}
export interface CreateBonusConceptRequest { name: string; }
export interface UpdateBonusConceptRequest { name: string; }

export type PayrollBonusAmountType = 1 | 2; // 1 FixedAmount, 2 Percentage
export const PAYROLL_BONUS_AMOUNT_TYPES: { value: PayrollBonusAmountType; label: string }[] = [
  { value: 1, label: 'Monto fijo' },
  { value: 2, label: 'Porcentaje del sueldo base' },
];

export interface PayrollBonusResponse {
  id: string;
  employeeId: string;
  conceptId: string;
  amountType: PayrollBonusAmountType;
  value: number;
  notes: string | null;
  status: number; // 1 Pending, 2 Applied, 3 Cancelled
  payrollLiquidationId: string | null;
}
export interface CreatePayrollBonusRequest {
  employeeId: string;
  conceptId: string;
  amountType: PayrollBonusAmountType;
  value: number;
  notes: string | null;
}
```

Then modify the existing `PayrollLiquidationResponse` interface to add the new field (keep every other field exactly as-is):

```typescript
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
  bonusLines: PayrollLiquidationLineResponse[];
}
```

- [ ] **Step 2: `payroll-bonus-concept.service.ts`**

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BonusConceptResponse, CreateBonusConceptRequest, UpdateBonusConceptRequest } from '../models/payroll.models';

@Injectable({ providedIn: 'root' })
export class PayrollBonusConceptService {
  private readonly base = `${environment.apiUrl}/payroll-bonus-concepts`;

  constructor(private readonly http: HttpClient) {}

  list(activeOnly: boolean): Observable<BonusConceptResponse[]> {
    return this.http.get<BonusConceptResponse[]>(this.base, { params: { activeOnly: String(activeOnly) } });
  }

  create(req: CreateBonusConceptRequest): Observable<BonusConceptResponse> {
    return this.http.post<BonusConceptResponse>(this.base, req);
  }

  update(id: string, req: UpdateBonusConceptRequest): Observable<BonusConceptResponse> {
    return this.http.put<BonusConceptResponse>(`${this.base}/${id}`, req);
  }

  setActive(id: string, isActive: boolean): Observable<BonusConceptResponse> {
    return this.http.put<BonusConceptResponse>(`${this.base}/${id}/active`, { isActive });
  }
}
```

- [ ] **Step 3: `payroll-bonus.service.ts`**

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreatePayrollBonusRequest, PayrollBonusResponse } from '../models/payroll.models';

@Injectable({ providedIn: 'root' })
export class PayrollBonusService {
  private readonly base = `${environment.apiUrl}/payroll-bonuses`;

  constructor(private readonly http: HttpClient) {}

  list(employeeId?: string, status?: number): Observable<PayrollBonusResponse[]> {
    let params = new HttpParams();
    if (employeeId) {
      params = params.set('employeeId', employeeId);
    }
    if (status) {
      params = params.set('status', String(status));
    }
    return this.http.get<PayrollBonusResponse[]>(this.base, { params });
  }

  create(req: CreatePayrollBonusRequest): Observable<PayrollBonusResponse> {
    return this.http.post<PayrollBonusResponse>(this.base, req);
  }

  cancel(id: string): Observable<PayrollBonusResponse> {
    return this.http.post<PayrollBonusResponse>(`${this.base}/${id}/cancel`, {});
  }
}
```

- [ ] **Step 4: Build to verify**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: 0 errors. (The `PayrollLiquidationResponse.bonusLines` addition will NOT break `liquidations.component.ts`/`.html` yet — Task 4 updates those; TypeScript structural typing means existing code that doesn't read `bonusLines` still compiles fine with the extra field present.)

- [ ] **Step 5: Commit**

```bash
git add src/app/core/models/payroll.models.ts src/app/core/services/payroll-bonus-concept.service.ts src/app/core/services/payroll-bonus.service.ts
git commit -m "feat(payroll): modelos y servicios de bonificaciones"
```

---

### Task 2: Bonus concepts ABM screen

**Files:**
- Create: `src/app/features/payroll/bonus-concepts/bonus-concepts.component.ts`
- Create: `src/app/features/payroll/bonus-concepts/bonus-concepts.component.html`
- Create: `src/app/features/payroll/bonus-concepts/bonus-concepts.component.css`

**Interfaces:**
- Consumes: `PayrollBonusConceptService` (Task 1), `BonusConceptResponse`/`CreateBonusConceptRequest`/`UpdateBonusConceptRequest` (Task 1), `PermissionCodes.payrollManage` (existing), `ToastService` (existing).
- Produces: `BonusConceptsComponent` (standalone), consumed by Task 5's route registration.

This screen is a near-verbatim copy of `src/app/features/payroll/deduction-concepts/deduction-concepts.component.{ts,html,css}`, with the `percentage` field removed (bonus concepts are just a name — the amount/type live on each per-employee assignment, not on the concept itself) and copy/labels changed from "descuento" to "bonificación".

- [ ] **Step 1: `bonus-concepts.component.ts`**

```typescript
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { BonusConceptResponse } from '../../../core/models/payroll.models';
import { PermissionCodes } from '../../../core/models/permission.models';
import { AuthService } from '../../../core/services/auth.service';
import { PayrollBonusConceptService } from '../../../core/services/payroll-bonus-concept.service';
import { ToastService } from '../../../shared/services/toast.service';

type ConceptView = {
  concept: BonusConceptResponse;
  expanded: boolean;
};

@Component({
  selector: 'app-bonus-concepts',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './bonus-concepts.component.html',
  styleUrls: ['./bonus-concepts.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BonusConceptsComponent implements OnInit {
  readonly permissionCodes = PermissionCodes;

  createForm: FormGroup;
  editForm: FormGroup;
  concepts: ConceptView[] = [];
  editingConcept: BonusConceptResponse | null = null;
  savingCreate = false;
  savingEdit = false;
  savingToggle = false;

  constructor(
    private readonly fb: FormBuilder,
    private readonly service: PayrollBonusConceptService,
    public readonly auth: AuthService,
    private readonly toast: ToastService,
    private readonly cdr: ChangeDetectorRef
  ) {
    this.createForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(150)]]
    });
    this.editForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(150)]]
    });
  }

  ngOnInit(): void {
    this.loadConcepts();
  }

  get canManage(): boolean {
    return this.auth.hasPermission(PermissionCodes.payrollManage);
  }

  loadConcepts(): void {
    this.service.list(false).subscribe({
      next: concepts => {
        this.concepts = concepts.map(concept => ({ concept, expanded: false }));
        this.cdr.markForCheck();
      },
      error: () => {
        this.toast.error('No se pudieron cargar los conceptos de bonificación');
        this.cdr.markForCheck();
      }
    });
  }

  toggleExpand(view: ConceptView): void {
    view.expanded = !view.expanded;
  }

  startEdit(view: ConceptView): void {
    if (!this.canManage) return;
    this.editingConcept = view.concept;
    this.editForm.setValue({ name: view.concept.name });
    view.expanded = true;
  }

  cancelEdit(): void {
    this.editingConcept = null;
    this.editForm.reset({ name: '' });
  }

  submitCreate(): void {
    if (!this.canManage || this.createForm.invalid || this.savingCreate) return;
    const payload = this.formPayload(this.createForm);
    this.savingCreate = true;
    this.service.create(payload).subscribe({
      next: () => {
        this.createForm.reset({ name: '' });
        this.savingCreate = false;
        this.loadConcepts();
        this.toast.success('Concepto creado');
        this.cdr.markForCheck();
      },
      error: () => {
        this.savingCreate = false;
        this.toast.error('Error al crear concepto');
        this.cdr.markForCheck();
      }
    });
  }

  submitEdit(): void {
    if (!this.canManage || !this.editingConcept || this.editForm.invalid || this.savingEdit) return;
    const id = this.editingConcept.id;
    this.savingEdit = true;
    this.service.update(id, this.formPayload(this.editForm)).subscribe({
      next: updated => {
        this.replaceConcept(updated);
        this.editingConcept = null;
        this.savingEdit = false;
        this.toast.success('Concepto actualizado');
        this.cdr.markForCheck();
      },
      error: () => {
        this.savingEdit = false;
        this.toast.error('Error al actualizar concepto');
        this.cdr.markForCheck();
      }
    });
  }

  toggleActive(view: ConceptView): void {
    if (!this.canManage || this.savingToggle) return;
    this.savingToggle = true;
    this.service.setActive(view.concept.id, !view.concept.isActive).subscribe({
      next: updated => {
        this.replaceConcept(updated);
        this.savingToggle = false;
        this.toast.success(updated.isActive ? 'Concepto activado' : 'Concepto desactivado');
        this.cdr.markForCheck();
      },
      error: () => {
        this.savingToggle = false;
        this.toast.error('Error al actualizar el estado del concepto');
        this.cdr.markForCheck();
      }
    });
  }

  trackById(_index: number, view: ConceptView): string {
    return view.concept.id;
  }

  private formPayload(form: FormGroup): { name: string } {
    return { name: String(form.get('name')?.value ?? '').trim() };
  }

  private replaceConcept(updated: BonusConceptResponse): void {
    this.concepts = this.concepts.map(view =>
      view.concept.id === updated.id ? { ...view, concept: updated } : view
    );
  }
}
```

- [ ] **Step 2: `bonus-concepts.component.html`**

```html
<div class="page">
  <header class="hero">
    <div class="hero__copy">
      <div class="eyebrow">_ SUELDOS</div>
      <span class="hero__kicker">Conceptos de bonificación</span>
      <h1>Conceptos de bonificación</h1>
      <p>Gestiona los tipos de bonificación disponibles para asignar a los empleados (presentismo, bonificación por venta, etc.).</p>
    </div>
  </header>

  <section class="panel panel--create" *ngIf="canManage">
    <div class="panel__header panel__header--create">
      <div>
        <span class="panel__eyebrow">Etapa 1</span>
        <span>Nuevo concepto</span>
      </div>
    </div>
    <form [formGroup]="createForm" (ngSubmit)="submitCreate()" class="inline-form">
      <label class="field field--grow">
        <span class="field__label">Nombre</span>
        <input class="control" type="text" maxlength="150" placeholder="Presentismo" formControlName="name" />
      </label>
      <button class="btn btn--primary" type="submit" [disabled]="createForm.invalid || savingCreate">
        {{ savingCreate ? 'Guardando...' : 'Crear concepto' }}
      </button>
    </form>
  </section>

  <section class="panel panel--list">
    <div class="panel__header">
      <div>
        <span class="panel__eyebrow">Etapa 2</span>
        <span>Conceptos</span>
      </div>
      <span class="panel__count">{{ concepts.length }}</span>
    </div>

    <div class="empty" *ngIf="concepts.length === 0">No hay conceptos registrados.</div>

    <div class="concept-list" *ngIf="concepts.length > 0">
      <article class="concept-item" *ngFor="let view of concepts; trackBy: trackById" [class.concept-item--expanded]="view.expanded">
        <div class="concept-row" (click)="toggleExpand(view)">
          <div class="concept-row__info">
            <div class="concept-row__primary">
              <span class="concept-row__name">{{ view.concept.name }}</span>
            </div>
            <span [class]="view.concept.isActive ? 'badge badge--in' : 'badge badge--out'"
                  [class.badge--toggle]="canManage"
                  title="Clic para cambiar estado"
                  (click)="$event.stopPropagation(); toggleActive(view)">
              {{ view.concept.isActive ? 'Activo' : 'Inactivo' }}
            </span>
          </div>

          <div class="concept-row__actions" (click)="$event.stopPropagation()">
            <button class="btn btn--ghost btn--sm" type="button" *ngIf="canManage" (click)="startEdit(view)">Editar</button>
            <svg class="chevron" [class.chevron--open]="view.expanded" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="6 9 12 15 18 9"/>
            </svg>
          </div>
        </div>

        <div class="concept-expand" *ngIf="view.expanded">
          <section class="expand-section concept-edit" *ngIf="editingConcept?.id === view.concept.id">
            <div class="expand-section__head">
              <span class="expand-section__eyebrow">Concepto</span>
              <strong>Editar nombre</strong>
            </div>

            <form [formGroup]="editForm" (ngSubmit)="submitEdit()" class="edit-form">
              <label class="edit-field edit-field--grow">
                <span class="edit-field__label">Nombre</span>
                <input class="control" type="text" maxlength="150" formControlName="name" />
              </label>
              <div class="edit-form__actions">
                <button class="btn btn--primary" type="submit" [disabled]="editForm.invalid || savingEdit">Guardar</button>
                <button class="btn btn--ghost" type="button" (click)="cancelEdit()">Cancelar</button>
              </div>
            </form>
          </section>
        </div>
      </article>
    </div>
  </section>
</div>
```

- [ ] **Step 3: `bonus-concepts.component.css`**

Copy `src/app/features/payroll/deduction-concepts/deduction-concepts.component.css` verbatim to `bonus-concepts.component.css` — no changes needed. Angular component styles are view-encapsulated (scoped per-component), so identical class names (`.concept-row`, `.concept-item`, etc.) in two different components never collide; there is nothing in that CSS file that references the `percentage` field that was removed from the HTML.

```bash
cp src/app/features/payroll/deduction-concepts/deduction-concepts.component.css src/app/features/payroll/bonus-concepts/bonus-concepts.component.css
```

- [ ] **Step 4: Build to verify**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: 0 errors. (This component isn't routed yet — Task 5 does that — but it must still compile standalone.)

- [ ] **Step 5: Commit**

```bash
git add src/app/features/payroll/bonus-concepts/
git commit -m "feat(payroll): ABM de conceptos de bonificacion"
```

---

### Task 3: Bonuses screen (create, list, cancel per employee)

**Files:**
- Create: `src/app/features/payroll/bonuses/bonuses.component.ts`
- Create: `src/app/features/payroll/bonuses/bonuses.component.html`
- Create: `src/app/features/payroll/bonuses/bonuses.component.css`

**Interfaces:**
- Consumes: `PayrollBonusService`, `PayrollBonusConceptService` (Task 1), `EmployeeService.listEmployees()` (existing, same as `AdvancesComponent` uses), `SearchableSelectComponent` (existing shared component), `ConfirmationService`, `ToastService`, `PermissionCodes.payrollManage`.
- Produces: `BonusesComponent` (standalone), consumed by Task 5's route registration.

This screen mirrors `src/app/features/payroll/advances/advances.component.{ts,html,css}` closely, with these deliberate differences: no cash-drawer/cash-session integration (bonuses don't move cash — they only affect the liquidation's net amount when it's generated), an extra "Concepto" dropdown (from the active bonus concepts catalog), and an "Tipo" (amount type) dropdown that toggles whether the value field is a currency amount or a percentage — when percentage is selected, show a live hint of the resulting peso amount using the selected employee's `baseSalary`.

- [ ] **Step 1: `bonuses.component.ts`**

```typescript
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { EmployeeService } from '../../../core/services/employee.service';
import { AuthService } from '../../../core/services/auth.service';
import { PayrollBonusService } from '../../../core/services/payroll-bonus.service';
import { PayrollBonusConceptService } from '../../../core/services/payroll-bonus-concept.service';
import { EmployeeResponse } from '../../../core/models/employee.models';
import { BonusConceptResponse, PAYROLL_BONUS_AMOUNT_TYPES, PayrollBonusAmountType, PayrollBonusResponse } from '../../../core/models/payroll.models';
import { PermissionCodes } from '../../../core/models/permission.models';
import { SearchableSelectComponent, SearchableSelectOption } from '../../../shared/components/searchable-select/searchable-select.component';
import { ConfirmationService } from '../../../shared/services/confirmation.service';
import { ToastService } from '../../../shared/services/toast.service';

@Component({
  selector: 'app-bonuses',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, SearchableSelectComponent],
  templateUrl: './bonuses.component.html',
  styleUrls: ['./bonuses.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BonusesComponent implements OnInit {
  readonly amountTypes = PAYROLL_BONUS_AMOUNT_TYPES;
  readonly permissionCodes = PermissionCodes;
  readonly statusOptions = [
    { value: 1, label: 'Pendiente' },
    { value: 2, label: 'Aplicado' },
    { value: 3, label: 'Cancelado' }
  ];

  employees: EmployeeResponse[] = [];
  concepts: BonusConceptResponse[] = [];
  bonuses: PayrollBonusResponse[] = [];
  showCreate = false;
  loading = false;
  savingCreate = false;
  cancellingId: string | null = null;

  filterForm: FormGroup;
  createForm: FormGroup;

  constructor(
    private readonly fb: FormBuilder,
    private readonly employeesService: EmployeeService,
    private readonly bonusesService: PayrollBonusService,
    private readonly conceptsService: PayrollBonusConceptService,
    public readonly auth: AuthService,
    private readonly toast: ToastService,
    private readonly confirmation: ConfirmationService,
    private readonly cdr: ChangeDetectorRef
  ) {
    this.filterForm = this.fb.group({
      employeeId: [''],
      status: ['']
    });
    this.createForm = this.fb.group({
      employeeId: ['', Validators.required],
      conceptId: ['', Validators.required],
      amountType: [1, Validators.required],
      value: [null, [Validators.required, Validators.min(0.01)]],
      notes: ['']
    });

    this.createForm.get('amountType')?.valueChanges.subscribe(() => this.cdr.markForCheck());
    this.createForm.get('employeeId')?.valueChanges.subscribe(() => this.cdr.markForCheck());
    this.createForm.get('value')?.valueChanges.subscribe(() => this.cdr.markForCheck());
  }

  ngOnInit(): void {
    this.loadEmployees();
    this.loadConcepts();
    this.loadBonuses();
  }

  get canManageBonuses(): boolean {
    return this.auth.hasPermission(PermissionCodes.payrollManage);
  }

  get employeeOptions(): SearchableSelectOption[] {
    return this.employees.map(employee => ({
      value: employee.id,
      label: employee.fullName,
      meta: employee.employeeRoleName
    }));
  }

  get conceptOptions(): SearchableSelectOption[] {
    return this.concepts.filter(c => c.isActive).map(concept => ({
      value: concept.id,
      label: concept.name
    }));
  }

  get isPercentage(): boolean {
    return Number(this.createForm.get('amountType')?.value) === 2;
  }

  get percentagePreview(): string | null {
    if (!this.isPercentage) return null;
    const employeeId = this.createForm.get('employeeId')?.value;
    const value = Number(this.createForm.get('value')?.value);
    const employee = this.employees.find(e => e.id === employeeId);
    if (!employee || !employee.baseSalary || !value || value <= 0) return null;
    const amount = (employee.baseSalary * value) / 100;
    return `≈ ${amount.toLocaleString('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 })} sobre un sueldo base de ${employee.baseSalary.toLocaleString('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 })}`;
  }

  openCreateForm(): void {
    if (!this.canManageBonuses) return;
    this.showCreate = true;
    this.createForm.reset({
      employeeId: '',
      conceptId: '',
      amountType: 1,
      value: null,
      notes: ''
    });
  }

  closeCreateForm(): void {
    if (this.savingCreate) return;
    this.showCreate = false;
  }

  applyFilters(): void {
    this.loadBonuses();
  }

  submitCreate(): void {
    if (!this.canManageBonuses || this.createForm.invalid || this.savingCreate) {
      this.createForm.markAllAsTouched();
      return;
    }

    const raw = this.createForm.getRawValue();
    this.savingCreate = true;

    this.bonusesService.create({
      employeeId: raw.employeeId,
      conceptId: raw.conceptId,
      amountType: Number(raw.amountType) as PayrollBonusAmountType,
      value: Number(raw.value),
      notes: raw.notes?.trim() ? raw.notes.trim() : null
    }).subscribe({
      next: created => {
        this.bonuses = [created, ...this.bonuses];
        this.savingCreate = false;
        this.showCreate = false;
        this.toast.success('Bonificación creada');
        this.cdr.markForCheck();
      },
      error: error => {
        this.savingCreate = false;
        this.toast.error(this.errorMessage(error, 'No se pudo crear la bonificación'));
        this.cdr.markForCheck();
      }
    });
  }

  async cancelBonus(bonus: PayrollBonusResponse): Promise<void> {
    if (!this.canManageBonuses || bonus.status !== 1 || this.cancellingId) return;
    const confirmed = await this.confirmation.confirm({
      eyebrow: 'Bonificación de sueldo',
      title: 'Cancelar bonificación',
      message: `Se cancelará la bonificación de ${this.employeeName(bonus.employeeId)}.`,
      confirmLabel: 'Cancelar bonificación',
      tone: 'danger'
    });
    if (!confirmed) return;

    this.cancellingId = bonus.id;
    this.bonusesService.cancel(bonus.id).subscribe({
      next: updated => {
        this.replaceBonus(updated);
        this.cancellingId = null;
        this.toast.success('Bonificación cancelada');
        this.cdr.markForCheck();
      },
      error: error => {
        this.cancellingId = null;
        this.toast.error(this.errorMessage(error, 'No se pudo cancelar la bonificación'));
        this.cdr.markForCheck();
      }
    });
  }

  employeeName(employeeId: string): string {
    return this.employees.find(employee => employee.id === employeeId)?.fullName ?? employeeId;
  }

  conceptName(conceptId: string): string {
    return this.concepts.find(concept => concept.id === conceptId)?.name ?? conceptId;
  }

  amountLabel(bonus: PayrollBonusResponse): string {
    return bonus.amountType === 2
      ? `${bonus.value.toLocaleString('es-AR', { minimumFractionDigits: 2 })}%`
      : bonus.value.toLocaleString('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 });
  }

  statusLabel(status: number): string {
    return this.statusOptions.find(option => option.value === status)?.label ?? 'Estado';
  }

  statusBadgeClass(status: number): string {
    if (status === 2) return 'badge badge--in';
    if (status === 3) return 'badge badge--out';
    return 'badge badge--pending';
  }

  trackById(_index: number, bonus: PayrollBonusResponse): string {
    return bonus.id;
  }

  private loadEmployees(): void {
    this.employeesService.listEmployees().subscribe({
      next: employees => {
        this.employees = employees;
        this.cdr.markForCheck();
      },
      error: error => {
        this.toast.error(this.errorMessage(error, 'No se pudieron cargar los empleados'));
        this.cdr.markForCheck();
      }
    });
  }

  private loadConcepts(): void {
    this.conceptsService.list(false).subscribe({
      next: concepts => {
        this.concepts = concepts;
        this.cdr.markForCheck();
      },
      error: () => {
        this.toast.error('No se pudieron cargar los conceptos de bonificación');
        this.cdr.markForCheck();
      }
    });
  }

  private loadBonuses(): void {
    const raw = this.filterForm.getRawValue();
    const employeeId = raw.employeeId || undefined;
    const status = raw.status ? Number(raw.status) : undefined;
    this.loading = true;
    this.bonusesService.list(employeeId, status).subscribe({
      next: bonuses => {
        this.bonuses = bonuses;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: error => {
        this.loading = false;
        this.toast.error(this.errorMessage(error, 'No se pudieron cargar las bonificaciones'));
        this.cdr.markForCheck();
      }
    });
  }

  private replaceBonus(updated: PayrollBonusResponse): void {
    this.bonuses = this.bonuses.map(bonus => bonus.id === updated.id ? updated : bonus);
  }

  private errorMessage(error: unknown, fallback: string): string {
    const response = error as { error?: { detail?: string; message?: string; title?: string } } | null;
    return response?.error?.detail || response?.error?.message || response?.error?.title || fallback;
  }
}
```

- [ ] **Step 2: `bonuses.component.html`**

```html
<div class="page">
  <header class="hero">
    <div class="hero__copy">
      <div class="eyebrow">_ SUELDOS</div>
      <span class="hero__kicker">Bonificaciones</span>
      <h1>Bonificaciones</h1>
      <p>Asigná presentismo, bonificación por venta u otro concepto a un empleado. Se suman automáticamente al generar su próxima liquidación.</p>
    </div>
  </header>

  <section class="panel panel--list">
    <div class="panel__header">
      <div>
        <span class="panel__eyebrow">Filtros</span>
        <span>Bonificaciones registradas</span>
      </div>
      <button class="btn btn--primary btn--sm" type="button" *ngIf="canManageBonuses && !showCreate" (click)="openCreateForm()">Nueva bonificación</button>
    </div>

    <form [formGroup]="filterForm" (ngSubmit)="applyFilters()" class="inline-form">
      <label class="field field--grow">
        <span class="field__label">Empleado</span>
        <app-searchable-select formControlName="employeeId" [options]="employeeOptions" placeholder="Todos" searchPlaceholder="Buscar empleado..."></app-searchable-select>
      </label>
      <label class="field">
        <span class="field__label">Estado</span>
        <select class="control" formControlName="status">
          <option value="">Todos</option>
          <option *ngFor="let option of statusOptions" [value]="option.value">{{ option.label }}</option>
        </select>
      </label>
      <button class="btn btn--ghost" type="submit">Filtrar</button>
    </form>
  </section>

  <section class="panel panel--create" *ngIf="showCreate && canManageBonuses">
    <div class="panel__header panel__header--create">
      <div>
        <span class="panel__eyebrow">Nueva</span>
        <span>Cargar bonificación</span>
      </div>
      <button class="btn btn--ghost btn--sm" type="button" (click)="closeCreateForm()">Cerrar</button>
    </div>

    <form [formGroup]="createForm" (ngSubmit)="submitCreate()" class="advance-form">
      <label class="field field--grow">
        <span class="field__label">Empleado</span>
        <app-searchable-select formControlName="employeeId" [options]="employeeOptions" placeholder="Seleccionar empleado" searchPlaceholder="Buscar empleado..."></app-searchable-select>
      </label>
      <label class="field field--grow">
        <span class="field__label">Concepto</span>
        <app-searchable-select formControlName="conceptId" [options]="conceptOptions" placeholder="Seleccionar concepto" searchPlaceholder="Buscar concepto..."></app-searchable-select>
      </label>
      <label class="field">
        <span class="field__label">Tipo</span>
        <select class="control" formControlName="amountType">
          <option *ngFor="let type of amountTypes" [value]="type.value">{{ type.label }}</option>
        </select>
      </label>
      <label class="field">
        <span class="field__label">{{ isPercentage ? 'Porcentaje (%)' : 'Monto ($)' }}</span>
        <input class="control" type="number" step="0.01" min="0" [max]="isPercentage ? 100 : null" formControlName="value" />
      </label>
      <p class="field-hint" *ngIf="percentagePreview">{{ percentagePreview }}</p>
      <label class="field field--grow">
        <span class="field__label">Notas</span>
        <input class="control" type="text" maxlength="500" formControlName="notes" />
      </label>
      <button class="btn btn--primary" type="submit" [disabled]="createForm.invalid || savingCreate">
        {{ savingCreate ? 'Guardando...' : 'Crear bonificación' }}
      </button>
    </form>
  </section>

  <section class="panel">
    <div class="empty" *ngIf="!loading && bonuses.length === 0">No hay bonificaciones registradas.</div>

    <div class="advance-list" *ngIf="bonuses.length > 0">
      <article class="advance-row" *ngFor="let bonus of bonuses; trackBy: trackById">
        <div class="advance-row__info">
          <div class="advance-row__primary">
            <span class="advance-row__name">{{ employeeName(bonus.employeeId) }} · {{ conceptName(bonus.conceptId) }}</span>
            <span class="advance-amount">{{ amountLabel(bonus) }}</span>
          </div>
          <span [class]="statusBadgeClass(bonus.status)">{{ statusLabel(bonus.status) }}</span>
        </div>
        <div class="advance-row__actions">
          <button class="btn btn--ghost btn--sm" type="button" *ngIf="canManageBonuses" [disabled]="bonus.status !== 1 || cancellingId === bonus.id" (click)="cancelBonus(bonus)">Cancelar</button>
        </div>
      </article>
    </div>
  </section>
</div>
```

- [ ] **Step 3: `bonuses.component.css`**

Copy `src/app/features/payroll/advances/advances.component.css` verbatim to `bonuses.component.css`, then add one small rule for the `.field-hint` class used in the new percentage-preview line (this class doesn't exist in the advances sibling since it has no equivalent preview text):

```bash
cp src/app/features/payroll/advances/advances.component.css src/app/features/payroll/bonuses/bonuses.component.css
```

Then append to the end of the copied file:

```css
.field-hint {
  flex: 1 1 100%;
  margin: -0.4rem 0 0.2rem;
  font-family: 'Crimson Pro', serif;
  font-size: 0.82rem;
  color: var(--text-soft);
}
```

Note: `.advance-form` (which `bonuses.component.html`'s create form reuses via `class="advance-form"`) is `display: flex; flex-wrap: wrap;`, not CSS grid — `flex: 1 1 100%` is what forces the hint onto its own full-width line in a wrapping flex row, not `grid-column`.

- [ ] **Step 4: Build to verify**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/app/features/payroll/bonuses/
git commit -m "feat(payroll): pantalla de bonificaciones por empleado"
```

---

### Task 4: Wire bonuses into the liquidation detail view and PDF receipt

**Files:**
- Modify: `src/app/features/payroll/liquidations/liquidations.component.html`
- Modify: `src/app/features/payroll/liquidations/liquidations.component.ts`
- Modify: `src/app/features/payroll/liquidations/liquidations.component.css`

**Interfaces:**
- Consumes: `PayrollLiquidationResponse.bonusLines` (Task 1), the existing `lineTotal(...)` helper and `drawTable(...)` PDF helper already in `liquidations.component.ts` (no new helpers needed — bonuses reuse both exactly as deductions/advances do).

- [ ] **Step 1: Add the "Bonificaciones" block to the detail view**

In `liquidations.component.html`, inside the existing `<div class="detail-grid">` (around line 113), insert a new block between the "Descuentos" block and the "Adelantos" block (so the order reads Descuentos → Bonificaciones → Adelantos → Totales — deductions and additions grouped visually before the running total):

```html
              <div>
                <span class="expand-section__eyebrow">Bonificaciones</span>
                <p *ngIf="liquidation.bonusLines.length === 0">Sin bonificaciones.</p>
                <p *ngFor="let line of liquidation.bonusLines">{{ line.label }} · {{ line.amount | currency:'ARS':'symbol':'1.2-2' }}</p>
                <strong>Total bonificaciones: {{ lineTotal(liquidation.bonusLines) | currency:'ARS':'symbol':'1.2-2' }}</strong>
              </div>
```

The full `detail-grid` block should read, in order: Descuentos, Bonificaciones, Adelantos, Totales (4 children total). Do not change the existing Descuentos/Adelantos/Totales blocks — only insert the new one between them.

- [ ] **Step 2: Add the "Bonificaciones" table to the PDF receipt**

In `liquidations.component.ts`, find the line `drawTable('Descuentos', liquidation.deductionLines);` followed by `drawTable('Adelantos', liquidation.advanceLines);` (around line 469-470). Insert a bonus table call between them, matching the same order as Step 1:

```typescript
      drawHeader();
      drawInfo();
      drawTable('Descuentos', liquidation.deductionLines);
      drawTable('Bonificaciones', liquidation.bonusLines);
      drawTable('Adelantos', liquidation.advanceLines);
```

No other change is needed in the PDF code — `drawTable` already accepts any `Array<{label, amount}>` and `liquidation.bonusLines` matches that shape exactly (`PayrollLiquidationLineResponse[]`).

- [ ] **Step 3: Widen the detail grid to fit 4 columns**

In `liquidations.component.css`, find `.detail-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); ... }` (around line 340-344) and change the column count to 4:

```css
.detail-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1rem;
}
```

Leave the existing mobile override (`@media` block around line 465-467, `.detail-grid { grid-template-columns: 1fr; }`) unchanged — it already correctly collapses to a single column on narrow screens regardless of how many children the grid has.

- [ ] **Step 4: Build to verify**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: 0 errors. `liquidation.bonusLines` now resolves against the `PayrollLiquidationResponse` interface extended in Task 1.

- [ ] **Step 5: Commit**

```bash
git add src/app/features/payroll/liquidations/liquidations.component.html src/app/features/payroll/liquidations/liquidations.component.ts src/app/features/payroll/liquidations/liquidations.component.css
git commit -m "feat(payroll): mostrar bonificaciones en el detalle de liquidacion y el recibo PDF"
```

---

### Task 5: Routes and navigation

**Files:**
- Modify: `src/app/app.routes.ts`
- Modify: `src/app/shared/components/navbar/navbar.component.html`

**Interfaces:**
- Consumes: `BonusConceptsComponent` (Task 2), `BonusesComponent` (Task 3), `PermissionCodes.payrollManage` (existing).

- [ ] **Step 1: Add routes**

In `src/app/app.routes.ts`, find the `payroll/liquidations` route block (around line 149-155) and add two new route objects right after it (before the block's closing `},` that follows, matching the exact structure of the sibling `payroll/deduction-concepts` route above it):

```typescript
    {
        path: 'payroll/bonus-concepts',
        canActivate: [authGuard, permissionGuard],
        data: { permission: PermissionCodes.payrollManage },
        loadComponent: () =>
            import('./features/payroll/bonus-concepts/bonus-concepts.component').then(m => m.BonusConceptsComponent)
    },
    {
        path: 'payroll/bonuses',
        canActivate: [authGuard, permissionGuard],
        data: { permission: PermissionCodes.payrollManage },
        loadComponent: () =>
            import('./features/payroll/bonuses/bonuses.component').then(m => m.BonusesComponent)
    },
```

- [ ] **Step 2: Add navbar links**

In `src/app/shared/components/navbar/navbar.component.html`, inside the `<div class="sidebar__submenu" [class.is-open]="payrollMenuOpen">` block (starts around line 236), find the `payroll/liquidations` link (around line 264-270ish, the last item in this submenu) and add two new items right after it, before the submenu's closing `</div>`:

```html
          <a *ngIf="auth.hasPermission(permissionCodes.payrollManage)"
             class="sidebar__submenu-item" routerLink="/payroll/bonus-concepts" routerLinkActive="is-active">
            <svg class="sidebar__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
              <path d="M12 2L2 7l10 5 10-5-10-5z"/>
              <path d="M2 17l10 5 10-5"/>
              <path d="M2 12l10 5 10-5"/>
            </svg>
            <span>Conceptos de bonificación</span>
          </a>
          <a *ngIf="auth.hasPermission(permissionCodes.payrollManage)"
             class="sidebar__submenu-item" routerLink="/payroll/bonuses" routerLinkActive="is-active">
            <svg class="sidebar__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
              <path d="M20 12v6a2 2 0 01-2 2H6a2 2 0 01-2-2v-6"/>
              <path d="M2 7h20v5H2z"/>
              <path d="M12 22V7"/>
              <path d="M12 7a2.5 2.5 0 010-5C13.5 2 15 3.5 15 5v2h-3z"/>
              <path d="M12 7a2.5 2.5 0 000-5C10.5 2 9 3.5 9 5v2h3z"/>
            </svg>
            <span>Bonificaciones</span>
          </a>
```

Read the exact indentation/whitespace of the existing `payroll/liquidations` link before inserting, so the new lines match the surrounding file's formatting exactly (mirror indentation level, don't guess).

- [ ] **Step 3: Build to verify**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/app/app.routes.ts src/app/shared/components/navbar/navbar.component.html
git commit -m "feat(payroll): rutas y navegacion de bonificaciones"
```

---

### Task 6: Final manual verification

**Files:** none (verification only).

- [ ] **Step 1: Full build**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: 0 errors (warnings from pre-existing third-party deps like canvg/jspdf/exceljs are expected and unrelated).

- [ ] **Step 2: Manual smoke test in the browser**

Start (or confirm already running) the dev server: `ng serve`. With the backend API also running (`develop` branch, migrations applied), log in and:
1. Navigate to Sueldos → Conceptos de bonificación. Create a concept (e.g. "Presentismo"). Confirm it appears in the list, can be edited, and can be deactivated/reactivated.
2. Navigate to Sueldos → Bonificaciones. Create a bonus for an employee that has `baseSalary` configured: try both "Monto fijo" and "Porcentaje del sueldo base" — confirm the percentage preview text shows the right computed amount. Confirm the new bonus appears in the list as "Pendiente", and can be cancelled (status flips to "Cancelado", button disables).
3. Navigate to Sueldos → Liquidaciones, generate a period that includes the employee with a pending bonus, and confirm: the generated liquidation's net amount includes the bonus, the detail view's "Bonificaciones" column shows it, and downloading the PDF receipt includes a "Bonificaciones" table.

Report the outcome of this manual check — do not mark the plan complete without having actually clicked through it, per this project's rule that UI features must be verified in a browser, not just compiled.

- [ ] **Step 3: Report**

Summarize: new routes (`/payroll/bonus-concepts`, `/payroll/bonuses`), new nav items, and confirmation that the liquidation detail/PDF now include bonuses end-to-end.
