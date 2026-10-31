# Descarga de factura / NC / ND desde la cuenta corriente — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Desde el detalle de venta de la cuenta corriente del cliente se descargan la factura, la nota de crédito y la nota de débito con el mismo PDF fiscal que Gestión de Ventas.

**Architecture:** Solo front. Las reglas de "cuándo se puede descargar" pasan a funciones puras compartidas y la descarga (pedir el print + generar el PDF + avisar errores) a un servicio único `SaleFiscalPdfService`. Gestión de Ventas y la CC usan las dos cosas; no se copia lógica. El back ya expone todo (`GET /sales/{id}/invoice|credit-note|debit-note/print`, permiso `sales.access`) y `getSaleById` ya trae `invoicingStatus` y `debitNoteStatus`.

**Tech Stack:** Angular 16 standalone, RxJS 7, TypeScript strict, Jasmine/Karma.

**Spec:** `docs/superpowers/specs/2026-10-29-notas-recargo-descuento-design.md` (repo `eiti`), sección **4**.

## Global Constraints

- Repo: `C:/EiTeFront/eiti-front`. Trabajar en un worktree propio: `C:/EiTeFront/eiti-front/.claude/worktrees/notas-recargo-descuento`, rama `feature/notas-recargo-descuento` salida de `origin/develop` (nunca del HEAD actual del checkout, que está en `main`).
- Mismo PDF que Gestión de Ventas: `SaleService.getInvoicePrint` / `getCreditNotePrint` / `getDebitNotePrint` → `InvoicePdfService.generate`.
- Mismo permiso que Gestión de Ventas para ver los botones: `PermissionCodes.salesInvoice`.
- Mensajes de error, textuales (son los de Gestión de Ventas hoy): primero `error?.error?.detail`; si no hay, `'No se pudo obtener la factura.'` / `'No se pudo obtener la nota de crédito.'` / `'No se pudo obtener la nota de débito.'`; si falla la generación del PDF, `'No se pudo generar el PDF de la factura.'` / `'No se pudo generar el PDF de la nota de crédito.'` / `'No se pudo generar el PDF de la nota de débito.'`.
- Textos de los botones en la CC: `↓ Factura`, `↓ Nota de crédito`, `↓ Nota de débito`, con la clase `detalle-remito-btn` de sus vecinos ("↓ Remito sin precios").
- TypeScript strict, sin `any`; modelos y tipos en `core/models/sale.models.ts`; `customer-account.component` es `OnPush`.
- Antes de tocar la UI (Task 4) invocar `/frontend-design` (CLAUDE.md); la decisión esperada es reusar `detalle-remito-btn`, sin estilos nuevos.
- Verificación final: `ng build --configuration development` sin errores y `ng test --watch=false --browsers=ChromeHeadless` en verde.
- Commit y push a la rama; sin deploy (el usuario verifica en pantalla).

## Review Focus

- Venta con factura **en trámite (2) o rechazada (4)**: no aparece ningún botón (no hay comprobante autorizado). → test en Task 1 (tabla de estados).
- Venta **anulada pero con la NC todavía sin autorizar** (estado sigue en 3): aparece Factura, **no** Nota de crédito. → test en Task 1 y Task 4.
- Usuario **sin `sales.invoice`**: en la CC no ve ninguno de los tres botones aunque la venta esté facturada. → test en Task 4.
- El back responde error al pedir el print (p. ej. comprobante que el servicio fiscal no encuentra): toast con el `detail` del back, sin PDF. → test en Task 2.
- Falla la generación del PDF (p. ej. no carga el logo): toast con el mensaje de generación. → test en Task 2.

---

### Task 1: Worktree, tipo del comprobante y reglas de descarga compartidas

**Files:**
- Modify: `src/app/core/models/sale.models.ts` (≈ línea 160, `SaleInvoicePrintResponse.kind`)
- Create: `src/app/shared/utils/sale-fiscal-documents.util.ts`
- Test: `src/app/shared/utils/sale-fiscal-documents.util.spec.ts`

**Interfaces:**
- Produces:
  - `export type SaleFiscalDocumentKind = 'invoice' | 'creditNote' | 'debitNote';` en `sale.models.ts`, y `SaleInvoicePrintResponse.kind: SaleFiscalDocumentKind`.
  - `canDownloadSaleInvoice(invoicingStatus: SaleInvoicingStatus | null | undefined): boolean` → `true` para 3 (facturada) y 5 (anulada: la factura existió y se reimprime).
  - `canDownloadSaleCreditNote(invoicingStatus: SaleInvoicingStatus | null | undefined): boolean` → `true` solo para 5.
  - `canDownloadSaleDebitNote(debitNoteStatus: SaleInvoicingStatus | null | undefined): boolean` → `true` solo para 3 (ND autorizada).

- [ ] **Step 1: Crear el worktree del front**

```bash
cd C:/EiTeFront/eiti-front
git fetch origin
git worktree add .claude/worktrees/notas-recargo-descuento -b feature/notas-recargo-descuento origin/develop
cd .claude/worktrees/notas-recargo-descuento
cmd //c mklink /J node_modules C:\\EiTeFront\\eiti-front\\node_modules
```

Expected: worktree en `develop` (`git log --oneline -1` = `origin/develop`). `package.json` de `main` y `develop` son iguales, así que el junction a `node_modules` sirve; si alguna vez difieren, `npm ci` en el worktree.

- [ ] **Step 2: Escribir el test que falla** — `sale-fiscal-documents.util.spec.ts`

```ts
describe('sale-fiscal-documents.util', () => {
  const cases: [SaleInvoicingStatus | null | undefined, boolean, boolean][] = [
    // estado de facturación, factura, nota de crédito
    [null, false, false], [undefined, false, false],
    [1, false, false], [2, false, false], [3, true, false], [4, false, false], [5, true, true]
  ];
  cases.forEach(([status, invoice, creditNote]) => {
    it(`estado ${status}: factura ${invoice}, NC ${creditNote}`, () => {
      expect(canDownloadSaleInvoice(status)).toBe(invoice);
      expect(canDownloadSaleCreditNote(status)).toBe(creditNote);
    });
  });

  it('la ND se descarga solo autorizada', () => {
    expect(canDownloadSaleDebitNote(3)).toBeTrue();
    [null, undefined, 1, 2, 4, 5].forEach(s => expect(canDownloadSaleDebitNote(s as SaleInvoicingStatus | null | undefined)).toBeFalse());
  });
});
```

- [ ] **Step 3: Correr el test y verlo fallar**

Run: `npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/shared/utils/sale-fiscal-documents.util.spec.ts`
Expected: FAIL (no compila: las funciones no existen).

- [ ] **Step 4: Implementar** el tipo `SaleFiscalDocumentKind` (y usarlo en `SaleInvoicePrintResponse.kind`) y las tres funciones en `sale-fiscal-documents.util.ts`, usando `SALE_INVOICING_STATUS` en vez de números sueltos. Cada función con un comentario de una línea con el porqué (como los que tiene hoy `sales-page.component.ts`: "Una factura anulada sigue teniendo comprobante: se puede reimprimir.").

- [ ] **Step 5: Correr el test y verlo pasar** — mismo comando, Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/app/core/models/sale.models.ts src/app/shared/utils/sale-fiscal-documents.util.ts src/app/shared/utils/sale-fiscal-documents.util.spec.ts
git commit -m "feat(facturacion): reglas compartidas para descargar factura, NC y ND"
```

---

### Task 2: Servicio único de descarga del PDF fiscal

**Files:**
- Create: `src/app/shared/services/sale-fiscal-pdf.service.ts`
- Test: `src/app/shared/services/sale-fiscal-pdf.service.spec.ts`

**Interfaces:**
- Consumes: `SaleFiscalDocumentKind` (Task 1); `SaleService.getInvoicePrint/getCreditNotePrint/getDebitNotePrint(id: string): Observable<SaleInvoicePrintResponse>`; `InvoicePdfService.generate(print: SaleInvoicePrintResponse): Promise<void>`; `ToastService.error(message: string)`.
- Produces: `@Injectable({ providedIn: 'root' }) export class SaleFiscalPdfService` con `constructor(saleService: SaleService, invoicePdf: InvoicePdfService, toast: ToastService)` y `download(saleId: string, kind: SaleFiscalDocumentKind): void`.

- [ ] **Step 1: Escribir los tests que fallan** — construir el servicio con `new SaleFiscalPdfService(saleSpy, pdfSpy, toastSpy)` (spies de Jasmine, como `customer-account.component.spec.ts`):
  - `download('sale-1', 'invoice')` llama `getInvoicePrint('sale-1')` y `generate(print)` con el print devuelto; ídem `'creditNote'` → `getCreditNotePrint`, `'debitNote'` → `getDebitNotePrint`.
  - Si el print falla con `{ error: { detail: 'La venta no tiene factura.' } }` → `toast.error('La venta no tiene factura.')` y `generate` no se llama.
  - Si falla sin `detail` en `'creditNote'` → `toast.error('No se pudo obtener la nota de crédito.')`.
  - Si `generate` rechaza en `'debitNote'` → `toast.error('No se pudo generar el PDF de la nota de débito.')` (usar `fakeAsync` + `flushMicrotasks()`).

- [ ] **Step 2: Correr y verlo fallar**

Run: `npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/shared/services/sale-fiscal-pdf.service.spec.ts`
Expected: FAIL (el servicio no existe).

- [ ] **Step 3: Implementar `download`** con un mapa `kind → { fetch, fetchError, generateError }` usando los mensajes textuales de Global Constraints. Un solo `subscribe` (no anidado): `next` llama `generate(...).catch(...)`, `error` hace `toast.error(error?.error?.detail ?? fetchError)`. Tipar el error del `subscribe` sin `any` (p. ej. `{ error?: { detail?: string } } | null`).

- [ ] **Step 4: Correr y verlo pasar** — mismo comando, Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/app/shared/services/sale-fiscal-pdf.service.ts src/app/shared/services/sale-fiscal-pdf.service.spec.ts
git commit -m "feat(facturacion): servicio único para descargar el PDF de factura, NC y ND"
```

---

### Task 3: Gestión de Ventas usa las reglas y el servicio compartidos

**Files:**
- Modify: `src/app/features/sales/sales-page.component.ts` (`canDownloadInvoice` ≈ 2038, `downloadDebitNotePdf` ≈ 2308, `canDownloadCreditNote` ≈ 2323, `downloadInvoicePdf` ≈ 2327, `downloadCreditNotePdf` ≈ 2335, constructor ≈ 245)
- Test: `src/app/features/sales/sales-page.component.spec.ts`

**Interfaces:**
- Consumes: `canDownloadSaleInvoice`, `canDownloadSaleCreditNote` (Task 1); `SaleFiscalPdfService.download` (Task 2).
- Produces: nada nuevo. Los métodos públicos del componente **mantienen nombre y firma** (el template y el menú de acciones los usan): `canDownloadInvoice(sale)`, `canDownloadCreditNote(sale)`, `downloadInvoicePdf(sale)`, `downloadCreditNotePdf(sale)`, `downloadDebitNotePdf(sale)`.

- [ ] **Step 1: Escribir los tests que fallan** en `sales-page.component.spec.ts` (usa TestBed; el servicio nuevo es `providedIn: 'root'`, se espía con `spyOn(TestBed.inject(SaleFiscalPdfService), 'download')`):
  - `component.downloadInvoicePdf(sale)` → `download(sale.id, 'invoice')`; ídem `downloadCreditNotePdf` → `'creditNote'`, `downloadDebitNotePdf` → `'debitNote'`.

- [ ] **Step 2: Correr y verlo fallar**

Run: `npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/features/sales/sales-page.component.spec.ts`
Expected: FAIL (los métodos todavía llaman a `SaleService` directo).

- [ ] **Step 3: Implementar**: los tres `download*Pdf` delegan en `saleFiscalPdf.download(sale.id, kind)`; `canDownloadInvoice`/`canDownloadCreditNote` delegan en las funciones de Task 1 con `sale.invoicingStatus`. Inyectar `SaleFiscalPdfService`; si `InvoicePdfService` queda sin uso en el componente, sacarlo del constructor y del import. **No** cambiar la regla de la ND de esta pantalla (`fiscalReceivers(sale)?.debitNote`): el listado no trae `debitNoteStatus`.

- [ ] **Step 4: Correr y verlo pasar** — mismo comando, Expected: PASS (los tests existentes del spec también).

- [ ] **Step 5: Commit**

```bash
git add src/app/features/sales/sales-page.component.ts src/app/features/sales/sales-page.component.spec.ts
git commit -m "refactor(ventas): descarga de comprobantes fiscales por el servicio compartido"
```

---

### Task 4: Botones de Factura / NC / ND en el detalle de venta de la cuenta corriente

**Files:**
- Modify: `src/app/features/clients/customer-account.component.ts` (constructor ≈ 80-92; junto a `descargarRemitoTraslado`)
- Modify: `src/app/features/clients/customer-account.component.html` (≈ 139-141, `modal-head__actions` del detalle)
- Test: `src/app/features/clients/customer-account.component.spec.ts`

**Interfaces:**
- Consumes: `SaleFiscalDocumentKind`, `canDownloadSaleInvoice`, `canDownloadSaleCreditNote`, `canDownloadSaleDebitNote` (Task 1); `SaleFiscalPdfService.download` (Task 2); `detalleModalSale: SaleByIdResponse | null` (ya existe, trae `invoicingStatus` y `debitNoteStatus`).
- Produces, en `CustomerAccountComponent`:
  - constructor con `private readonly saleFiscalPdf: SaleFiscalPdfService` como **último** parámetro.
  - `get canDownloadDetalleInvoice(): boolean`, `get canDownloadDetalleCreditNote(): boolean`, `get canDownloadDetalleDebitNote(): boolean` → `auth.hasPermission(PermissionCodes.salesInvoice)` **y** la regla de Task 1 sobre `detalleModalSale` (falso si no hay venta cargada).
  - `downloadDetalleFiscalPdf(kind: SaleFiscalDocumentKind): void` → si hay `detalleModalSale`, `saleFiscalPdf.download(detalleModalSale.id, kind)`; si no, nada.

- [ ] **Step 1: Invocar `/frontend-design`** y confirmar que los botones reusan `detalle-remito-btn` sin CSS nuevo.

- [ ] **Step 2: Escribir los tests que fallan** en `customer-account.component.spec.ts`. Agregar un spy `SaleFiscalPdfService` (`['download']`) como último argumento en **todos** los `new CustomerAccountComponent(` del spec. Tests (con `component.detalleModalSale = { id: 'sale-1', invoicingStatus: X, debitNoteStatus: Y, ... } as SaleByIdResponse`):
  - permiso sí, `invoicingStatus: 3`, sin ND → Factura `true`, NC `false`, ND `false`.
  - permiso sí, `invoicingStatus: 5`, `debitNoteStatus: 3` → las tres `true`.
  - `auth.hasPermission` devuelve `false` para `'sales.invoice'`, `invoicingStatus: 5` → las tres `false`.
  - `detalleModalSale = null` → las tres `false` y `downloadDetalleFiscalPdf('invoice')` no llama a `download`.
  - `downloadDetalleFiscalPdf('creditNote')` con venta cargada → `download('sale-1', 'creditNote')`.

- [ ] **Step 3: Correr y verlo fallar**

Run: `npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/features/clients/customer-account.component.spec.ts`
Expected: FAIL.

- [ ] **Step 4: Implementar** los getters, el método y el constructor; en el template, después de "↓ Remito sin precios":

```html
<button class="detalle-remito-btn" type="button" *ngIf="canDownloadDetalleInvoice" title="Descargar la factura en PDF" (click)="downloadDetalleFiscalPdf('invoice')">↓ Factura</button>
<button class="detalle-remito-btn" type="button" *ngIf="canDownloadDetalleCreditNote" title="Descargar la nota de crédito en PDF" (click)="downloadDetalleFiscalPdf('creditNote')">↓ Nota de crédito</button>
<button class="detalle-remito-btn" type="button" *ngIf="canDownloadDetalleDebitNote" title="Descargar la nota de débito en PDF" (click)="downloadDetalleFiscalPdf('debitNote')">↓ Nota de débito</button>
```

- [ ] **Step 5: Correr y verlo pasar** — mismo comando, Expected: PASS.

- [ ] **Step 6: Verificación completa**

Run: `npx ng build --configuration development` → Expected: build sin errores (los warnings de `canvg`/`jspdf` son previos).
Run: `npx ng test --watch=false --browsers=ChromeHeadless` → Expected: todos los tests en verde.

- [ ] **Step 7: Commit y push**

```bash
git add src/app/features/clients/customer-account.component.ts src/app/features/clients/customer-account.component.html src/app/features/clients/customer-account.component.spec.ts
git commit -m "feat(cuenta-corriente): descargar factura, NC y ND desde el detalle de la venta"
git push -u origin feature/notas-recargo-descuento
```

Prueba en pantalla (la hace el usuario; no hay e2e local posible para comprobantes reales porque el print los lee del servicio fiscal de producción): en `/clients-cc/customer/:id`, abrir una venta facturada desde el extracto → "↓ Factura" baja el mismo PDF que en Gestión de Ventas.
