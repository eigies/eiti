# Cuenta Corriente de clientes en "modo bolsa" (espejo de proveedores)

## Objetivo
Llevar la CC de clientes al mismo modelo que proveedores: **bolsa por cliente** (saldo global,
débitos = ventas CC / créditos = cobros), con **cobro a nivel cliente** imputado **FIFO** a las ventas
pendientes, y la misma trazabilidad cobro→venta (desglose A, resumen al cobrar B, aviso al anular D).

## Decisiones cerradas con el usuario
1. **Espejo completo** (no orquestador liviano): nuevo `CustomerPayment` a nivel cliente + FIFO +
   caja/cheque/tarjeta al nivel del cobro + UI bolsa.
2. **Reemplazar** el flujo por-venta (`/sales-cc/:id/payments`) por la bolsa. Los cobros viejos por-venta
   quedan **legacy** (visibles, sin desglose).

## Hechos del código (verificados)
- `SalePaymentMethod.CustomerCredit = 6` ya existe → método de las imputaciones internas FIFO (front ya lo
  rotula "Saldo a favor"). Cuentan en `Sale.CcPaidTotal` y transicionan la venta a Paid.
- CC se gatea con `PermissionCodes.SalesPay` (cobrar) / `SalesAccess` (ver). **No hace falta permiso nuevo**
  ni tocar las 3 listas.
- `Customer.CreditBalance` + `AddCredit/ConsumeCredit` ya existen (hoy solo se genera al sobre-pagar UNA
  venta; no se auto-imputa). La bolsa lo va a auto-imputar como en proveedores.
- `Sale`: `IsCuentaCorriente`, `CcPaidTotal` (suma CcPayments activos), `CcPendingAmount`, `AddCcPayment`,
  `CancelCcPayment`. La confirmación de stock ocurre cuando la venta pasa a Paid.
- Caja CC actual (en `AddCcPaymentGroup`): efectivo→cajón; transferencia/tarjeta visibles pero excluidas del
  esperado; cheque/otro registrados; creación de `Cheque` (`CreateForCcPayment`); recargo de tarjeta por plan
  del banco. `CancelCcPayment` revierte por método + revierte crédito + revierte stock.
- gpt5-5 ya agregó a `CashMovement` referencias `SupplierPaymentId`/`SaleCcPaymentId` y el `CashSessionMapper`
  deriva el desglose desde movimientos → el patrón a seguir para `CustomerPaymentId`.

## Blueprint: reusar el feature de proveedores casi 1:1
- Dominio: `CustomerPayment` ↔ `SupplierPayment`; `CustomerCreditApplicator` ↔ `SupplierCreditApplicator`.
- App: `AddCustomerPayment`/`CancelCustomerPayment` ↔ `AddSupplierPayment`/`CancelSupplierPayment`;
  `ListCustomerAccounts`/`GetCustomerAccount` ↔ `ListSupplierAccounts`/`GetSupplierAccount`.
- Front: `customer-account.component` ↔ `supplier-account.component` (plantilla casi idéntica).
- A/B/D salen gratis encima, igual que en proveedores.

---

## FASE 1 — Dominio + entidad + migración estructural (bajo riesgo, nada cableado)
- [ ] `eiti.Domain/Customers/CustomerPayment.cs`: Id, CompanyId, CustomerId, BranchId, Method
      (SalePaymentMethod), Amount, Date, Notes, Status (Active/Cancelled), CreatedAt, CreatedByUserId,
      ChequeId?, y campos de tarjeta (CardBankId, CardCuotas, CardSurchargePct, CardSurchargeAmt,
      TotalCobrado). Métodos `Create(...)`, `Cancel()`, `SetCardData(...)`. (Espejo de SupplierPayment +
      campos de tarjeta de SaleCcPayment.)
- [ ] `SaleCcPayment`: agregar `Guid? CustomerPaymentId` + parámetro opcional en `Create(...)` (back-link de
      las imputaciones FIFO con el cobro que las generó).
- [ ] EF: `CustomerPaymentConfiguration` + `DbSet<CustomerPayment>` + mapear `SaleCcPayment.CustomerPaymentId`
      (`decimal(18,2)`, índices por CustomerId/Status).
- [ ] `ICustomerPaymentRepository` (+ impl + DI).
- [ ] `ISaleRepository.ListPendingCcSalesByCustomerAsync(companyId, customerId)` — ventas CC activas con
      pendiente > 0, **más vieja primero** (fuente FIFO). + `ListCcSalesByCustomerAsync` para la bolsa.
- [ ] Migración estructural: tabla `CustomerPayments` + columna `SaleCcPayments.CustomerPaymentId`.
      Generar desde `eiti.Infrastructure`. **No aplicar a prod sin OK.**
- [ ] Build con dependencias.

## FASE 2 — FIFO + comandos con caja/cheque/tarjeta/stock
- [ ] `CustomerCreditApplicator.ApplyToPendingCcSalesAsync(...)` (espejo del de proveedores): consume
      `CreditBalance`, crea `SaleCcPayment` método `CustomerCredit` con `CustomerPaymentId`, devuelve
      imputaciones `[{ saleId, code, amount }]` + ventas que pasaron a Paid (para confirmar stock).
- [ ] `AddCustomerPayment` (gated `SalesPay`): registra `CustomerPayment` real (método/cheque/tarjeta/caja).
      - Resolver caja abierta (`CashDrawerAccessPolicy`).
      - Cheque: validar/crear (referencia al CustomerPayment); recargo de tarjeta por plan del banco.
      - Caja: **un asiento por cobro** según método (nuevos métodos `RegisterCustomerPaymentIncome` /
        por-método, espejo de los CC income; efectivo al esperado, transfer/tarjeta visibles excluidos).
      - `customer.AddCredit(monto)` + applicator FIFO; excedente queda como saldo a favor.
      - Confirmar stock de las ventas que pasaron a Paid.
- [ ] `CancelCustomerPayment` (gated `SalesPay`): cancela las imputaciones (ventas vuelven a pendiente),
      descuenta del `CreditBalance` el sobrante que dejó, revierte caja, devuelve cheque, revierte stock.
- [ ] Caja: `CashMovement.CustomerPaymentId` + `CashReferenceTypes.CustomerPayment` + métodos en
      `CashSession` + mapear en `CashSessionMapper` (que los cobros entren en los contadores/desglose).
      Migración de columna+índice de caja. **No aplicar a prod sin OK.**
- [ ] Controller: endpoints `GET customer-accounts`, `GET customers/{id}/account`,
      `POST customers/{id}/payments`, `DELETE customers/{id}/payments/{paymentId}`.
- [ ] Retirar endpoints de cobro por-venta (`AddCcPaymentGroup`/`CancelCcPayment`) o dejarlos solo-lectura
      para legacy (a decidir al llegar).
- [ ] Build con dependencias.

## FASE 3 — Queries (bolsa + desglose)
- [ ] `ListCustomerAccounts` (gated `SalesAccess`): clientes con `saldoPendiente` (deuda CC) + `saldoAFavor`.
- [ ] `GetCustomerAccount` (gated `SalesAccess`): cabecera (deuda/cobrado/saldo/saldo a favor) + movimientos
      unificados (ventas CC débitos + CustomerPayment créditos + cobros legacy por-venta) + **desglose A**
      por cobro (imputaciones + sobrante), calculado desde los `SaleCcPayment` con `CustomerPaymentId`.
- [ ] **B** — `AddCustomerPaymentResponse` incluye `Imputaciones`.
- [ ] Build con dependencias.

## FASE 4 — Frontend (reusar supplier-account)
- [ ] `customer-account.models.ts` + `customer-account.service.ts` (list/get/addPayment/cancelPayment).
- [ ] `customer-accounts` (lista de clientes con saldo) + `customer-account` (bolsa) — plantilla de
      `supplier-account`. Incluye A (fila de cobro expandible), B (toast), D (confirm con ventas que vuelven).
- [ ] Rutas: `clients-cc` → lista; `clients-cc/customer/:customerId` → bolsa. Retirar
      `sales-cc/:id/payments`. Navbar apunta a la lista.
- [ ] `ng build --configuration development`.

## FASE 5 — Datos legacy + verificación e2e
- [ ] Cobros viejos por-venta visibles como legacy (sin desglose), igual que en proveedores.
- [ ] Flujo manual: lista de clientes → bolsa → cobrar parcial (FIFO a la venta más vieja) → cheque entero →
      sobre-cobro a saldo a favor → anular (ventas vuelven a pendiente, caja/cheque/stock revertidos) →
      caja muestra contadores correctos.

## Caveat (igual que proveedores)
Anular un cobro revierte sus imputaciones directas y el sobrante de saldo a favor. Si ese saldo ya fue
consumido por una venta posterior, `CreditBalance` puede quedar inconsistente (misma limitación que hoy y que
en proveedores). Se documenta.

## Riesgo principal
La caja: tocar `CashSession`/`CashMovement`/`CashSessionMapper` es el área que gpt5-5 acaba de estabilizar.
Hay que sumar el caso CustomerPayment sin romper los contadores existentes. Es la parte de mayor cuidado y la
que más tests amerita.
