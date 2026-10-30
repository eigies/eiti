# Recargo de tarjeta en la factura — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> ⚠️ **No se mergea a `main` sin el OK del contador** sobre el tratamiento fiscal del recargo (IVA incluido en el total facturado). Se construye y prueba en la rama.

**Goal:** El interés de las cuotas se calcula solo sobre lo que se paga con tarjeta, se cobra sin dejar saldo pendiente, entra en el total de la factura con su renglón propio y no cuenta como ganancia.

**Architecture:** Cambia el contrato del pago con tarjeta: el `amount` de la línea de tarjeta en el request es la **base** (la parte de la venta que paga esa tarjeta). El back busca el plan, calcula `recargo = round(base × pct / 100, 2)` y guarda `SalePayment.Amount = base + recargo`, `CardSurchargeAmt = recargo`, `Sale.CardSurchargeTotal = recargo`. Una sola tarjeta por venta. La factura se pide por `TotalAmount + CardSurchargeTotal`; el comprobante guarda el total que se pidió (`SaleFiscalDocument.RequestedTotal`), y con eso se sabe si una venta facturada antes de cobrar quedó con "recargo no facturado" (opción (a) decidida).

**Tech Stack:** .NET 10, MediatR, EF Core + Npgsql, xUnit · Angular 16, Jasmine/Karma, jsPDF.

**Spec:** `docs/superpowers/specs/2026-10-29-notas-recargo-descuento-design.md`, sección **2** (incluida la decisión (a)).

## Global Constraints

- Mismos worktrees y rama que el resto (`feature/notas-recargo-descuento` en back y front). Commits chicos, push a la rama.
- **Fórmula:** `recargo = CardSurchargeCalculator.Compute(base, plan.SurchargePct)`; ejemplo del spec: venta $100.000, $40.000 efectivo + MOURA 3 cuotas 11,06 % → base $60.000, recargo $6.636, posnet $66.636, cobrado $106.636, factura $106.636.
- **Una sola tarjeta por venta:** un request con más de una línea de tarjeta → `Error.Validation("Sales.Payments.SingleCard", "Solo se admite un pago con tarjeta por venta.")`. Hoy `BuildPayments` las fusiona en silencio: eso se reemplaza por el error.
- El back nunca toma un monto de recargo del front.
- **Factura:** `FiscalAmounts.Total = TotalAmount + CardSurchargeTotal`; con recargo > 0 el IVA se calcula sobre ese total a la alícuota de la venta (no se reusa `Sale.VatAmount`, que es solo de la mercadería).
- **`SaleFiscalDocument.RequestedTotal : decimal?`**: total que se pidió autorizar; se fija al armar el pedido de factura. Comprobantes viejos: null = se facturó `TotalAmount` (antes nunca se facturaba el recargo).
- **Recargo no facturado** = `max(0, TotalAmount + CardSurchargeTotal − (RequestedTotal ?? TotalAmount))` cuando hay factura vigente; se expone en `GetSaleById` y `ListSales` como `uninvoicedCardSurcharge` y el front muestra "Recargo no facturado $X".
- **Guarda de edición** (`UpdateSaleHandler`): sigue comparando la mercadería (`TotalAmount`) contra lo facturado; un cobro con tarjeta sobre una venta ya facturada se acepta (opción (a)).
- **PDF:** renglón de ajuste `Recargo financiero tarjeta (<Banco>, <N> cuotas, <X>%)` con su monto, solo si el comprobante autorizado lo incluye (total impreso − `TotalAmount` = `CardSurchargeTotal`).
- **Ganancia:** el recargo no entra en ingresos/ganancia de reportes de ventas ni dashboard (verificar que usan `TotalAmount` o renglones, no `EffectiveTotal`); los reportes de medios de pago y caja siguen mostrando lo cobrado (con recargo).
- Ventas CC y cobros de cuenta corriente: sin cambios.
- Migración única `AddFiscalDocumentRequestedTotal`.

## Review Focus

- Pago mixto efectivo + tarjeta en cuotas → la venta queda pagada, sin saldo pendiente. → test back + e2e.
- Dos líneas de tarjeta en el request → 400 claro, no se fusionan. → test back.
- Venta facturada en espera y cobrada después en cuotas → se acepta y aparece "Recargo no facturado". → test back + e2e.
- Edición de una venta en espera ya cobrada en parte con tarjeta → el front manda la base (no suma el recargo dos veces). → test front.
- Venta sin tarjeta → factura y PDF idénticos a hoy. → test back (BuildAmounts) y front (PDF sin renglón).

---

### Task B1: recargo por línea de tarjeta (CreateSale / UpdateSale)
Reemplazar el cálculo sobre el subtotal por el cálculo sobre la base de la línea; `SalePayment.Create(Card, base + recargo, …)` + `SetCardData(bank, cuotas, pct, recargo)`; error `SingleCard`; `CardSurchargeTotal` = recargo. Tests: ejemplo del spec, venta pagada sin saldo, `SingleCard`, plan sin recargo (0 %) → amount = base.

### Task B2: factura con recargo y "recargo no facturado"
`SaleInvoicingService.BuildAmounts` (total + IVA), `SaleFiscalDocument.RequestedTotal` (+ método de dominio, EF, migración) fijado al pedir la factura, `uninvoicedCardSurcharge` en lecturas, `SaleInvoicePrintAdjustments` con `CardSurcharge` y `CardSurchargeLabel`. Tests: BuildAmounts con/sin recargo, RequestedTotal guardado, venta facturada y cobrada después → uninvoiced = recargo, print con/sin renglón.

### Task B3: ganancia y reportes
Revisar `SalesReportHandler`, `DailySalesControl`, dashboard y caja: el recargo no suma a ventas/ganancia. Test que lo fije en el reporte de ventas.

### Task F1: pago con tarjeta en el front
`sale-payment-inline`: el monto de la línea de tarjeta es la base; recargo = base × pct; se muestra "A pasar por el posnet: $base+recargo (incluye recargo $X)"; `effectiveTotal = total + recargo`; una sola línea de tarjeta (el selector de medio no ofrece una segunda). `normalizeSalePayments` manda la base. Edición y "Cobrar" rápido cargan la base (`amount − cardSurchargeAmt`). Tests de cada punto.

### Task F2: factura y aviso
`invoice-pdf.service` dibuja el renglón del recargo; la tarjeta de Gestión de Ventas muestra "Recargo no facturado $X" cuando corresponde. Tests.

### Task E (e2e local, homologación; plan local Santander 3 cuotas 15 %)
Venta $4.000 con $1.000 efectivo + resto en 3 cuotas → posnet $3.450, venta pagada, Factura B por $4.450 con renglón de recargo; venta facturada en espera y cobrada después en cuotas → aviso "Recargo no facturado"; reporte de ventas sin el recargo en la ganancia.
