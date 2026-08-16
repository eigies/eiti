# Dashboard Monthly Reading Depth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dar volumen y jerarquía a la tabla "Lectura del mes en curso" sin modificar su contenido, márgenes ni comportamiento responsive.

**Architecture:** La mejora se resuelve en el componente existente del dashboard. Se agregan clases semánticas a los grupos de columnas Mes/Hoy y se estilizan las filas como bandas elevadas mediante CSS; no se modifica el flujo de datos ni el TypeScript.

**Tech Stack:** Angular, HTML, CSS, Jasmine/Karma.

## Global Constraints

- Mantener datos, cálculos, permisos y comportamiento actuales.
- Mantener el tamaño exterior y los márgenes actuales del panel.
- No agregar dependencias.
- Mantener la adaptación mobile existente.
- No hacer deploy.

---

### Task 1: Tratamiento elevado de la lectura mensual

**Files:**
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.html:73`
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.css:262`
- Test: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.spec.ts`

**Interfaces:**
- Consumes: `data.month`, `data.today` y `canViewFinancials` ya expuestos por `DashboardComponent`.
- Produces: clases de presentación `month-table__month` y `month-table__today`; no cambia interfaces TypeScript.

- [ ] **Step 1: Write the failing test**

Agregar un caso que renderice el resumen y verifique que una fila tenga esquinas redondeadas y que las celdas del mes y de hoy tengan fondos distintos:

```typescript
it('presenta la lectura mensual como filas elevadas y separa mes de hoy', async () => {
  const { fixture, component } = await setupFixture();
  component.summary = summary;
  component.loading = false;
  fixture.detectChanges();

  const row = fixture.nativeElement.querySelector('.month-table tbody tr') as HTMLElement;
  const monthCell = fixture.nativeElement.querySelector('.month-table__month') as HTMLElement;
  const todayCell = fixture.nativeElement.querySelector('.month-table__today') as HTMLElement;

  expect(Number.parseFloat(getComputedStyle(row).borderTopLeftRadius)).toBeGreaterThan(8);
  expect(getComputedStyle(monthCell).backgroundColor).not.toBe(getComputedStyle(todayCell).backgroundColor);
});
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
npm test -- --watch=false --include src/app/features/dashboard/dashboard.component.spec.ts
```

Expected: FAIL porque las filas no tienen radio visible y las clases de agrupación aún no existen.

- [ ] **Step 3: Write minimal implementation**

Agregar `month-table__month` a las celdas de ventas/importe del mes y `month-table__today` a las de hoy. Ajustar el CSS para usar separación vertical entre filas, fondos por grupo, borde y radio por fila, mayor jerarquía para Total y una versión mobile compatible con el grid existente.

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
npm test -- --watch=false --include src/app/features/dashboard/dashboard.component.spec.ts
```

Expected: todos los casos de `DashboardComponent` pasan.

- [ ] **Step 5: Run regression checks**

Run:

```powershell
npm test -- --watch=false
npm run build -- --configuration development
git diff --check
```

Expected: suite completa y build exitosos; `git diff --check` sin errores.

- [ ] **Step 6: Commit**

No crear commit de implementación salvo indicación explícita del usuario. No hacer push ni deploy.
