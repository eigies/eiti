# Dashboard Branch Selector And Selected Day Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reemplazar el selector nativo de sucursales y recuperar el indicador persistente del día elegido en el gráfico.

**Architecture:** `DashboardComponent` seguirá siendo dueño de `branchId` y `selectedDayKey`. El template delegará el dropdown a `SearchableSelectComponent` mediante ControlValueAccessor y representará el día activo con un chip derivado del estado ya existente; no se agregará estado duplicado.

**Tech Stack:** Angular 16 standalone components, FormsModule, Jasmine/Karma, CSS encapsulado.

## Global Constraints

- No desplegar.
- No crear commits.
- No modificar contratos de backend ni datos locales.
- Preservar todos los cambios no relacionados del worktree.

---

### Task 1: Dropdown compartido de sucursales

**Files:**
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.spec.ts`
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.ts`
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.html`
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.css`

**Interfaces:**
- Consumes: `SearchableSelectComponent`, `SearchableSelectOption`, `branchId`, `canViewAllBranches`, `setBranch(id: string | null)`.
- Produces: `branchOptions: SearchableSelectOption[]` y `onBranchSelected(value: string | number | null): void`.

- [ ] **Step 1: Escribir la prueba fallida**

Agregar una prueba de template que renderice `DashboardComponent` con dos sucursales y verifique que existe `app-searchable-select`, no existe `.branch-filter select`, y que seleccionar `branch-b` invoca el flujo que actualiza `branchId` y pide un nuevo resumen.

- [ ] **Step 2: Ejecutar la prueba y comprobar RED**

Run: `npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/features/dashboard/dashboard.component.spec.ts`

Expected: FAIL porque el template todavía contiene un `<select>` nativo.

- [ ] **Step 3: Implementar lo mínimo**

Importar `FormsModule`, `SearchableSelectComponent` y `SearchableSelectOption`; exponer las opciones con `{ value: branch.id, label: branch.name }`; normalizar el valor seleccionado a `string | null`; reemplazar el HTML por:

```html
<app-searchable-select
  [ngModel]="branchId"
  (ngModelChange)="onBranchSelected($event)"
  [options]="branchOptions"
  [allowClear]="canViewAllBranches"
  clearLabel="Todas"
  [clearValue]="null"
  [compact]="true"
  placeholder="Todas las sucursales"
  searchPlaceholder="Buscar sucursal..."
  ariaLabel="Sucursal del dashboard">
</app-searchable-select>
```

Actualizar selectores CSS de `.branch-filter select` para dimensionar el host `app-searchable-select` sin duplicar los estilos internos del componente compartido.

- [ ] **Step 4: Ejecutar la prueba y comprobar GREEN**

Run: `npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/features/dashboard/dashboard.component.spec.ts`

Expected: PASS.

### Task 2: Indicador persistente del día seleccionado

**Files:**
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.spec.ts`
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.html`
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.css`

**Interfaces:**
- Consumes: `selectedDayKey`, `selectedDayLabel`, `clearDay()` y las clases `chart-day.is-selected`/`is-dimmed`.
- Produces: `.dashboard-scope` y `.scope-chip` visibles durante el filtro actual.

- [ ] **Step 1: Escribir la prueba fallida**

Agregar una prueba de template que seleccione `2026-08-13`, verifique un `.scope-chip` con la fecha formateada, una `.chart-day.is-selected`, y que el botón del chip ejecute `clearDay()` y elimine el indicador.

- [ ] **Step 2: Ejecutar la prueba y comprobar RED**

Run: `npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/features/dashboard/dashboard.component.spec.ts`

Expected: FAIL porque el rediseño ya no renderiza el chip superior.

- [ ] **Step 3: Implementar lo mínimo**

Insertar el chip antes del primer panel del contenido cargado:

```html
<div class="dashboard-scope" *ngIf="selectedDayKey">
  <span class="scope-chip">
    {{ selectedDayLabel }}
    <button type="button" (click)="clearDay()" [attr.aria-label]="'Quitar filtro del ' + selectedDayLabel">×</button>
  </span>
</div>
```

Agregar estilos responsive para el chip y devolver a `.chart-day.is-selected .chart-bar` un gradiente, borde y sombra claramente visibles.

- [ ] **Step 4: Ejecutar la prueba y comprobar GREEN**

Run: `npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/features/dashboard/dashboard.component.spec.ts`

Expected: PASS.

### Task 3: Verificación integral

**Files:**
- Verify only: `C:/EiTeFront/eiti-front`

**Interfaces:**
- Consumes: implementación de Tasks 1 y 2.
- Produces: evidencia de regresión, compilación y comportamiento visual.

- [ ] **Step 1: Ejecutar toda la suite**

Run: `npm test -- --watch=false --browsers=ChromeHeadless`

Expected: todas las pruebas PASS, 0 failed.

- [ ] **Step 2: Compilar development**

Run: `npm run build -- --configuration development`

Expected: exit code 0.

- [ ] **Step 3: Revisar visualmente en local**

Abrir el dashboard local con Legacy Company, confirmar que el dropdown compartido abre/busca/limpia y que al pulsar una barra aparecen simultáneamente la barra resaltada y el chip, sin desborde en desktop ni mobile.
