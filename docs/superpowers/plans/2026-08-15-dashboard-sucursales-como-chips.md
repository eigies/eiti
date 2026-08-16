# Dashboard Branch Chips Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reemplazar el dropdown de sucursales del dashboard por chips consistentes con los toggles del gráfico.

**Architecture:** `DashboardComponent` conserva `branchId` y `setBranch` como única fuente de verdad. El template representa directamente el alcance global y las sucursales como botones, sin ControlValueAccessor ni estado duplicado.

**Tech Stack:** Angular 16 standalone components, Jasmine/Karma, CSS encapsulado.

## Global Constraints

- Mobile usa una sola fila con scroll horizontal.
- No modificar backend ni el componente compartido.
- No crear commits ni desplegar.

---

### Task 1: Comportamiento de los chips

**Files:**
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.spec.ts`
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.ts`
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.html`

**Interfaces:**
- Consumes: `branches`, `branchId`, `canViewAllBranches`, `setBranch(id: string | null)`.
- Produces: botones `.branch-chip` con `aria-pressed`.

- [ ] **Step 1: Escribir la prueba fallida**

Actualizar el spec de selector para exigir tres chips (`Todas`, `Centro`, `Norte`), ausencia de `app-searchable-select`, estado activo y cambio real al pulsar `Norte`. Eliminar el spec de stacking del dropdown.

- [ ] **Step 2: Verificar RED**

Run: `npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/features/dashboard/dashboard.component.spec.ts`

Expected: FAIL porque todavía se renderiza `app-searchable-select`.

- [ ] **Step 3: Implementar lo mínimo**

Reemplazar el control por un grupo de botones:

```html
<div class="branch-filter" *ngIf="branches.length > 1">
  <span>Sucursal</span>
  <div class="branch-chips" role="group" aria-label="Sucursal del dashboard">
    <button *ngIf="canViewAllBranches" type="button" class="branch-chip" [class.is-active]="branchId === null" [attr.aria-pressed]="branchId === null" (click)="setBranch(null)">Todas</button>
    <button *ngFor="let branch of branches" type="button" class="branch-chip" [class.is-active]="branchId === branch.id" [attr.aria-pressed]="branchId === branch.id" (click)="setBranch(branch.id)">{{ branch.name }}</button>
  </div>
</div>
```

Eliminar imports, getters y handlers exclusivos del dropdown.

- [ ] **Step 4: Verificar GREEN**

Run: `npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/features/dashboard/dashboard.component.spec.ts`

Expected: PASS.

### Task 2: Layout y verificación

**Files:**
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.css`

**Interfaces:**
- Consumes: `.branch-filter`, `.branch-chips`, `.branch-chip`, `.is-active`.
- Produces: wrap en desktop y scroll horizontal en mobile.

- [ ] **Step 1: Implementar estilos**

Reutilizar tamaños, bordes, tipografía y estado activo de `.panel-toggle`; en `max-width: 760px`, usar `display:flex`, `flex-wrap:nowrap`, `overflow-x:auto` y `white-space:nowrap`.

- [ ] **Step 2: Ejecutar suite completa**

Run: `npm test -- --watch=false --browsers=ChromeHeadless`

Expected: todas las pruebas PASS.

- [ ] **Step 3: Compilar development**

Run: `npm run build -- --configuration development`

Expected: exit code 0.
