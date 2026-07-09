# Módulo de pago de salarios (Payroll) — Diseño

Fecha: 2026-07-09
Estado: Aprobado, pendiente de plan de implementación

## Contexto y alcance

Hoy `eiti` no tiene ningún concepto de sueldo/liquidación de empleados. `Employee`
(`eiti.Domain/Employees/Employee.cs`) solo tiene datos de contacto y rol. Se pidió
un módulo nuevo de "pago de salario a empleados".

Alcance acordado (full payroll, no un simple registro de pagos):

- Sueldo base fijo por período, configurable por empleado.
- Periodicidad configurable por empleado (mensual o quincenal).
- Adelantos de sueldo, que se descuentan automáticamente en la próxima liquidación.
- Descuentos por porcentaje (aportes/ART/obra social, etc.), definidos como catálogo
  a nivel empresa y aplicados automáticamente a todas las liquidaciones — el % lo
  carga el usuario, el sistema no calcula fórmulas legales.
- Generación de liquidaciones en lote por período, para todos los empleados activos
  con esa periodicidad.
- El pago puede tocar caja (efectivo, como un retiro) o no (transferencia/otro —
  solo queda registrado).
- Recibo de sueldo en PDF, generado client-side como el resto de los documentos
  del sistema.

Explícitamente fuera de alcance de esta v1:

- Cálculo automático de cargas sociales según fórmulas legales (el usuario carga
  los % a mano).
- Sueldo por hora/día trabajado o registro de asistencia.
- Periodicidad distinta a mensual/quincenal.

## Decisión de arquitectura

Se evaluaron 3 opciones:

- **A (elegida):** Entidades independientes (`PayrollLiquidation` como aggregate
  root por empleado-período, sin un aggregate "período" contenedor). Consistente
  con cómo ya están modelados `Purchase` y `Sale` en este código — agregados
  chicos e independientes, no contenedores gigantes.
- **B (descartada):** Un `PayrollPeriod` aggregate que contiene todas las
  liquidaciones del mes como entidades hijas. Con 50-200 empleados, cada
  `SaveChanges` cargaría/grabaría un aggregate enorme — no escala y rompe el
  patrón del resto del proyecto.
- **C (descartada):** Desglose de la liquidación en JSON (como `AuditLog.PayloadJson`),
  sin tablas tipadas para adelantos/descuentos. Pierde validación fuerte y la
  posibilidad de queries SQL simples como "adelantos pendientes de Juan".

## Modelo de dominio

### `Employee` (extendido)

- `BaseSalary: decimal?` — sueldo base fijo del período. `null` = el empleado no
  está en payroll.
- `PayrollPeriodicity: PayrollPeriodicity?` — enum `Monthly = 1, Biweekly = 2`.

### `PayrollDeductionConcept` (catálogo por empresa)

- `Id, CompanyId, Name, Percentage, IsActive, CreatedAt`.
- CRUD simple (crear/editar/desactivar), mismo patrón que `Bank`.

### `PayrollAdvance` (adelanto)

- `Id, CompanyId, EmployeeId, Amount, Date, Notes, Status, AppliedToLiquidationId?, CreatedByUserId, CreatedAt`.
- `Status`: `Pending = 1, Applied = 2, Cancelled = 3`.
- `Pending`: aún no se descontó de ninguna liquidación.
- `Applied`: ya se restó en una liquidación (`AppliedToLiquidationId` guarda cuál,
  para trazabilidad — mismo patrón que `PurchasePayment.SupplierPaymentId`).
- Solo se puede cancelar un adelanto `Pending`.
- Se puede crear en cualquier momento y opcionalmente pagarse ya en efectivo
  (afecta caja al momento de crearse, no al liquidar).

### `PayrollLiquidation` (aggregate root, una por empleado por período)

- `Id, CompanyId, EmployeeId, BranchId?, PeriodLabel (ej. "2026-07"), PeriodStart, PeriodEnd, GrossAmount, Status, PaymentMethod?, PaidAt?, CashSessionId?, CreatedAt`.
- `Status`: `Pending = 1, Paid = 2, Cancelled = 3`.
- `PaymentMethod`: `Cash = 1, Transfer = 2, Other = 3`.
- Colecciones hijas (mismo patrón que `Purchase.Details`):
  - `_deductionLines: List<PayrollLiquidationDeductionLine>` — **snapshot** del
    catálogo al momento de generar (`ConceptName, Percentage, Amount`). No es una
    referencia viva: si el % del catálogo cambia después, las liquidaciones viejas
    no se recalculan.
  - `_advanceLines: List<PayrollLiquidationAdvanceLine>` — snapshot de los
    adelantos `Pending` aplicados (`PayrollAdvanceId, Amount`).
- `NetAmount => GrossAmount - deductionLines.Sum(Amount) - advanceLines.Sum(Amount)`
  (propiedad computada, mismo patrón que `Sale.PendingAmount`).

### Invariantes clave

- Único `PayrollLiquidation` no cancelado por `(EmployeeId, PeriodLabel)`.
- `MarkAsPaid` con `PaymentMethod == Cash` requiere `CashSessionId` de una sesión
  abierta con acceso validado.
- Cancelar una liquidación `Paid`:
  - revierte el movimiento de caja si tocó caja,
  - devuelve a `Pending` los `PayrollAdvance` que había aplicado,
  - rechaza con error de conflicto si la `CashSession` original ya está cerrada
    (no la reabre — mismo criterio que otras anulaciones del sistema).

## Flujo de aplicación (vertical slices)

Ubicación: `eiti.Application/Features/Payroll/{DeductionConcepts,Advances,Liquidations}/`.

**Catálogo de descuentos**
- `CreateDeductionConceptCommand` / `UpdateDeductionConceptCommand` — CRUD simple.
- `ListDeductionConceptsQuery`.

**Adelantos**
- `CreatePayrollAdvanceCommand(EmployeeId, Amount, Date, Notes, PaymentMethod, CashSessionId?)`
  — crea el adelanto en `Pending`; si `PaymentMethod == Cash`, descuenta de la caja
  indicada en el momento (mismo mecanismo que `CreateCashWithdrawal`: valida acceso
  vía `CashDrawerAccessPolicy`, requiere sesión abierta).
- `CancelPayrollAdvanceCommand` — solo si `Pending`; revierte el movimiento de caja
  si correspondía.
- `ListPayrollAdvancesQuery` — filtro por empleado/estado.

**Generación en lote**
- `GeneratePayrollPeriodCommand(Periodicity, PeriodLabel, PeriodStart, PeriodEnd)`.
- Handler: para cada empleado activo con `PayrollPeriodicity == Periodicity`,
  `BaseSalary != null` y sin liquidación no cancelada existente para ese
  `PeriodLabel`:
  1. `GrossAmount = employee.BaseSalary`.
  2. Snapshotea las líneas del catálogo de descuentos activo.
  3. Busca `PayrollAdvance` en `Pending` del empleado, las snapshotea como líneas
     de adelanto y las marca `Applied`.
  4. Crea el `PayrollLiquidation` en `Pending`.
- Todo en una transacción (`SaveChangesAsync` al final).
- Respuesta: cuántas se generaron y cuántos empleados se saltearon (con motivo:
  ya tenía liquidación / sin sueldo configurado).

**Pago**
- `PayLiquidationCommand(LiquidationId, PaymentMethod, CashSessionId?)`.
- `Cash`: valida acceso a la caja, registra `CashMovement` tipo
  `CashMovementType.PayrollExpense` (dirección `Out`, `ReferenceType =
  "PayrollLiquidation"`, `PayrollLiquidationId` seteado).
- `Transfer`/`Other`: solo marca `Paid`, sin tocar caja.

**Cancelación**
- `CancelLiquidationCommand(LiquidationId)` — revierte caja si correspondía,
  devuelve adelantos aplicados a `Pending`, marca `Cancelled`.

**Consultas**
- `ListLiquidationsQuery` (filtros período/empleado/estado) y
  `GetLiquidationByIdQuery` (detalle completo para el recibo).

**Permisos nuevos** (en las tres listas — `PermissionCodes.cs`, `RoleCatalog.cs`,
`PermissionCatalog.cs` — más el mapa del frontend):
`PayrollManage`, `PayrollLiquidationsGenerate`, `PayrollLiquidationsPay`,
`PayrollAdvancesManage`.

## Integración de caja

- Nuevos `CashMovementType`: `PayrollExpense = 15` (pago de liquidación en
  efectivo), `PayrollAdvanceExpense = 16` (adelanto pagado en efectivo) — se
  distinguen en los reportes de caja.
- `CashMovement` gana `PayrollLiquidationId: Guid?` y `PayrollAdvanceId: Guid?`
  (mismo patrón que `SupplierPaymentId`/`CustomerPaymentId`).
- `CashMovementsReportHandler` y `CashSessionMapper` suman estas dos categorías a
  sus arrays de categorías existentes — sin lógica nueva, solo entradas nuevas.
- Nuevo método de dominio `CashSession.RegisterPayrollExpense(...)`, análogo a
  `RegisterWithdrawal`, para no bypassear las invariantes de sesión abierta/balance.

## Frontend

Nueva feature `src/app/features/payroll/`, ruta lazy `/payroll` con sub-tabs
(mismo patrón que `sales-full` con sub-rutas):

- **Empleados**: extiende la grilla de empleados existente con Sueldo base /
  Periodicidad.
- **Descuentos**: ABM del catálogo (nombre + %), como `BanksComponent`.
- **Adelantos**: listado + alta (empleado, monto, fecha, medio de pago).
- **Liquidaciones**: grilla con filtro de período/estado, botón "Generar
  liquidaciones de [mes]" que dispara el batch y muestra el resumen
  (generadas/salteadas). Acciones por fila: Pagar / Cancelar / Ver recibo.

Modelos: `payroll.models.ts` (`PayrollLiquidation`, `PayrollAdvance`,
`PayrollDeductionConcept`) + `payroll.service.ts`. Sidebar: link "Sueldos" bajo
el grupo de Empleados, guardado por `PayrollManage`.

## Recibo PDF

Generado 100% client-side con jsPDF (mismo patrón que ventas/auditoría — sin
generación de PDF en el backend). Contenido: branding de empresa
(`PdfBrandingService`), datos del empleado, período, bruto, líneas de descuento,
líneas de adelantos aplicados, neto pagado, fecha y medio de pago. Botón "Ver
recibo" disponible en liquidaciones `Paid`.

## Casos borde

- Empleado sin `BaseSalary` → no entra en la generación en lote, se lista como
  salteado con motivo.
- Generar el mismo período dos veces → el batch detecta las existentes y las
  salta, no duplica.
- Cancelar una liquidación `Paid` en efectivo cuya `CashSession` de origen ya
  cerró → se rechaza con error de conflicto (no se reabre la caja), mismo
  criterio que otras anulaciones del sistema.

## Testing

- Unit tests de dominio: `PayrollLiquidation` (cálculo de neto, unicidad por
  período, snapshot de descuentos/adelantos), `PayrollAdvance` (transiciones de
  estado).
- Handler tests: generación en lote (saltea duplicados y empleados sin sueldo),
  pago (cash vs. transfer), cancelación (revierte adelantos y caja).
