# Bonificaciones de Sueldo (Payroll Bonuses) — Design Spec

## Contexto

El módulo de Sueldos (payroll) ya soporta:
- Sueldo base + periodicidad por empleado (`Employee.BaseSalary`, `Employee.PayrollPeriodicity`)
- Descuentos globales automáticos (`PayrollDeductionConcept`): un catálogo de conceptos con un `Percentage` fijo que se aplica a **todos** los empleados activos al generar el lote de liquidaciones.
- Adelantos por empleado (`PayrollAdvance`): se cargan ad-hoc, quedan `Pending`, y se barren automáticamente al generar la liquidación del empleado (se restan del neto).
- Liquidaciones (`PayrollLiquidation`): agregan `GrossAmount` (= sueldo base), líneas de descuento y líneas de adelanto. `NetAmount = GrossAmount - deducciones - adelantos`.

Falta: conceptos que **suman** al sueldo y que varían **por empleado** y **por período** — presentismo y bonificación por venta son los dos casos concretos hoy, pero el diseño debe permitir agregar otros sin tocar código (igual que el catálogo de descuentos).

## Decisiones ya tomadas (confirmadas con el usuario)

- Debe poder cargarse **por monto fijo o por porcentaje**.
- El porcentaje es **sobre el sueldo base del empleado** (no sobre ventas reales — eso queda fuera de alcance por ahora, evita integrar el módulo de Payroll con Ventas).
- Debe ser **asignable por empleado** (no automático/global como los descuentos): cada empleado puede tener un valor distinto, o no tener el concepto ese período.

## Alcance

**Incluye:**
- Catálogo de conceptos de bonificación (nombre, activo/inactivo), reutilizable entre empleados y períodos.
- Carga de bonificaciones pendientes por empleado (monto fijo o %), antes de generar la liquidación del período.
- Inclusión automática de las bonificaciones pendientes del empleado al generar su liquidación (mismo mecanismo que adelantos).
- Reversión a `Pending` si la liquidación se cancela.
- Pantalla de administración (ABM) del catálogo + pantalla de carga/listado de bonificaciones por empleado.

**Fuera de alcance (explícitamente):**
- Cálculo automático de presentismo en base a asistencias (no hay módulo de asistencia).
- Porcentaje calculado sobre ventas reales del período (requeriría integrar con `Sales`).
- Aplicación automática/masiva a todos los empleados (a diferencia de los descuentos).

## Modelo de dominio

### `PayrollBonusConcept` (nuevo aggregate, `eiti.Domain/Payroll/`)

Catálogo de tipos de bonificación, análogo a `PayrollDeductionConcept` pero sin porcentaje fijo (el valor se define por asignación, no por concepto).

```csharp
public sealed class PayrollBonusConcept : AggregateRoot<PayrollBonusConceptId>
{
    public CompanyId CompanyId { get; private set; }
    public string Name { get; private set; }        // "Presentismo", "Bonificación por venta"
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static PayrollBonusConcept Create(CompanyId companyId, string name);
    public void Update(string name);
    public void Activate();
    public void Deactivate();
}
```

Reglas: `Name` requerido, máx 150 caracteres, único por compañía (case-insensitive) — mismo criterio que `PayrollDeductionConcept`.

### `PayrollBonusAmountType` (nuevo enum)

```csharp
public enum PayrollBonusAmountType
{
    FixedAmount = 1,
    Percentage = 2
}
```

### `PayrollBonusStatus` (nuevo enum)

```csharp
public enum PayrollBonusStatus
{
    Pending = 1,
    Applied = 2,
    Cancelled = 3
}
```

Mismos tres estados que `PayrollAdvanceStatus`, mismo significado.

### `PayrollBonus` (nuevo aggregate, `eiti.Domain/Payroll/`)

Asignación concreta de un concepto a un empleado, análoga a `PayrollAdvance`.

```csharp
public sealed class PayrollBonus : AggregateRoot<PayrollBonusId>
{
    public CompanyId CompanyId { get; private set; }
    public EmployeeId EmployeeId { get; private set; }
    public PayrollBonusConceptId ConceptId { get; private set; }
    public PayrollBonusAmountType AmountType { get; private set; }
    public decimal Value { get; private set; }          // monto en $ o % (0-100) según AmountType
    public string? Notes { get; private set; }
    public PayrollBonusStatus Status { get; private set; }
    public PayrollLiquidationId? PayrollLiquidationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static PayrollBonus Create(CompanyId companyId, EmployeeId employeeId, PayrollBonusConceptId conceptId,
        PayrollBonusAmountType amountType, decimal value, string? notes);

    public void Apply(PayrollLiquidationId liquidationId);   // Pending -> Applied
    public void Cancel();                                     // Pending -> Cancelled (solo si Pending)
    public void RevertToPending();                            // Applied -> Pending (al cancelar la liquidación)

    public decimal Resolve(decimal employeeBaseSalary) =>
        AmountType == PayrollBonusAmountType.FixedAmount
            ? Value
            : decimal.Round(employeeBaseSalary * Value / 100m, 2, MidpointRounding.AwayFromZero);
}
```

Validaciones (constructor):
- `Value > 0`
- Si `AmountType == Percentage`, `Value` entre 0 y 100 (mismo límite que `PayrollDeductionConcept.Percentage`)
- `Notes` opcional, máx 500 caracteres (mismo patrón que `SaleTransportAssignment.Notes`)

### `PayrollLiquidationBonusLine` (nueva entidad hija, `eiti.Domain/Payroll/`)

Análoga a `PayrollLiquidationAdvanceLine`, pero guarda también el nombre del concepto y el tipo/valor originales para trazabilidad en el recibo PDF (igual que `PayrollLiquidationDeductionLine` guarda `ConceptName` + `Percentage`).

```csharp
public sealed class PayrollLiquidationBonusLine
{
    public Guid Id { get; private set; }
    public PayrollLiquidationId PayrollLiquidationId { get; private set; }
    public Guid PayrollBonusId { get; private set; }
    public string ConceptName { get; private set; }
    public PayrollBonusAmountType AmountType { get; private set; }
    public decimal Value { get; private set; }       // valor original (monto o %)
    public decimal Amount { get; private set; }       // monto resuelto en $

    public static PayrollLiquidationBonusLine Create(Guid payrollBonusId, string conceptName,
        PayrollBonusAmountType amountType, decimal value, decimal amount);

    internal void AttachToLiquidation(PayrollLiquidationId liquidationId);
}
```

### Cambios en `PayrollLiquidation`

```csharp
private readonly List<PayrollLiquidationBonusLine> _bonusLines = [];
public IReadOnlyCollection<PayrollLiquidationBonusLine> BonusLines => _bonusLines;

public decimal NetAmount =>
    GrossAmount
    + _bonusLines.Sum(l => l.Amount)
    - _deductionLines.Sum(l => l.Amount)
    - _advanceLines.Sum(l => l.Amount);
```

`Create(...)` gana un parámetro `IReadOnlyList<PayrollLiquidationBonusLine> bonusLines`, adjuntadas igual que las otras dos listas.

## Infraestructura (EF Core)

- `PayrollBonusConceptConfiguration` — calcado de `PayrollDeductionConceptConfiguration` (índice único `CompanyId` + `Name` case-insensitive).
- `PayrollBonusConfiguration` — calcado de `PayrollAdvanceConfiguration` (FKs a `Employee`, `PayrollBonusConcept`, `PayrollLiquidation` nullable; índice en `EmployeeId`, `Status`).
- `PayrollLiquidationConfiguration` — agrega la navegación `BonusLines` con `UsePropertyAccessMode(PropertyAccessMode.Field)`, mismo patrón que `DeductionLines`/`AdvanceLines`.
- Migración `AddPayrollBonuses`: tablas `PayrollBonusConcepts`, `PayrollBonuses`, `PayrollLiquidationBonusLines`.
- `decimal(18,2)` para `Value`/`Amount` en todas las tablas nuevas, siguiendo la convención del proyecto.

## Repositorios

- `IPayrollBonusConceptRepository` — calcado de `IPayrollDeductionConceptRepository` (`AddAsync`, `GetByIdAsync`, `ListByCompanyAsync(activeOnly)`, `ExistsByNameAsync`).
- `IPayrollBonusRepository` — calcado de `IPayrollAdvanceRepository` (`AddAsync`, `GetByIdAsync`, `ListPendingByEmployeeAsync`, `ListByCompanyAsync` con filtros).

## Aplicación (Features)

Estructura vertical-slice calcada de `Features/Payroll/DeductionConcepts` y `Features/Payroll/Advances`:

- `Features/Payroll/BonusConcepts/Commands/CreateBonusConcept`
- `Features/Payroll/BonusConcepts/Commands/UpdateBonusConcept`
- `Features/Payroll/BonusConcepts/Commands/SetBonusConceptActive`
- `Features/Payroll/BonusConcepts/Queries/ListBonusConcepts`
- `Features/Payroll/Bonuses/Commands/CreatePayrollBonus`
- `Features/Payroll/Bonuses/Commands/CancelPayrollBonus`
- `Features/Payroll/Bonuses/Queries/ListPayrollBonuses` (por empleado y/o estado)

Cada handler sigue el patrón ya establecido: `EnsureAuthenticated()` + guard explícito de `CompanyId` nulo, `Result<T>`, `*Errors.cs` con constantes.

### Cambios en `GeneratePayrollPeriodHandler`

Junto con `pendingAdvances`, buscar `pendingBonuses` del empleado:

```csharp
var pendingBonuses = await _bonusRepository.ListPendingByEmployeeAsync(companyId, employee.Id, cancellationToken);
var bonusLines = pendingBonuses
    .Select(bonus => PayrollLiquidationBonusLine.Create(
        bonus.Id.Value,
        bonusConceptNamesById[bonus.ConceptId],
        bonus.AmountType,
        bonus.Value,
        bonus.Resolve(employee.BaseSalary.Value)))
    .ToList();
```

Y tras crear la liquidación, `bonus.Apply(liquidation.Id)` por cada una (igual que `advance.Apply(...)`).

### Cambios en `CancelLiquidationHandler`

Junto con revertir adelantos a `Pending`, revertir bonificaciones: `foreach (var bonus in bonuses) bonus.RevertToPending();`.

### Permisos

Reutilizar `PayrollManage` (ABM de conceptos y carga de bonificaciones) — mismo nivel que `PayrollAdvancesManage` hoy. No se agregan permisos nuevos.

## Frontend

**Pantalla "Conceptos de bonificación"** (nueva, calcada de `deduction-concepts.component`): ABM simple (nombre, activo/inactivo).

**Pantalla "Bonificaciones"** (nueva, calcada de `advances.component`):
- Alta: selector de empleado + selector de concepto (dropdown, desde el catálogo activo) + selector de tipo (Monto fijo / Porcentaje) + input de valor (si es %, con hint "% sobre sueldo base $X" mostrando el sueldo base del empleado seleccionado) + notas opcionales.
- Listado: empleado, concepto, tipo, valor, estado (Pendiente/Aplicado/Cancelado), acción cancelar (solo si Pendiente).

**Pantalla "Liquidaciones" (`liquidations.component`):** el detalle de liquidación agrega una sección "Bonificaciones" (positiva) junto a "Descuentos" y "Adelantos aplicados", mismo estilo visual.

**Recibo PDF:** agrega línea(s) de bonificación antes del total, mismo formato que descuentos/adelantos pero en verde/positivo en vez de rojo/negativo (si el PDF actual distingue color; si no, alcanza con el signo `+`).

**Navegación:** dos ítems nuevos en el submenú "Sueldos" del navbar (mismo patrón que Adelantos/Conceptos de descuento hoy), gateados por `payroll.manage`.

## Edge cases

- Empleado sin `BaseSalary` configurado + bonificación por `%` pendiente → al generar el período, el empleado ya se salta por "Sin sueldo base configurado" (regla existente); la bonificación queda Pendiente sin aplicarse, se arrastra al próximo intento.
- Concepto desactivado con bonificaciones `Pending` ya cargadas → siguen aplicándose normalmente (la desactivación solo impide *nuevas* asignaciones, no afecta las existentes — mismo criterio que `PayrollDeductionConcept.IsActive` con `activeOnly` en el query de generación... salvo que acá el filtro de activo aplica al **catálogo para elegir en el combo**, no a bonificaciones ya cargadas).
- Cancelar una bonificación `Applied` (vinculada a una liquidación ya generada) → no se puede cancelar directamente; primero hay que cancelar la liquidación (que la revierte a `Pending`), igual que con adelantos.
- Bonificación tipo `Percentage` con `Value = 0` → rechazada por validación (`Value > 0`), evita cargar bonificaciones sin efecto por error.

## Testing

Mismo patrón que `PayrollAdvanceTests`/`PayrollAdvanceHandlersTests`:
- Dominio: `PayrollBonusTests` (creación, validaciones de `Value`/`AmountType`, transiciones de estado, `Resolve()` con monto fijo y con porcentaje).
- Dominio: `PayrollBonusConceptTests` (creación, update, activar/desactivar).
- Handlers: `PayrollBonusHandlersTests` (crear, cancelar, listar — incluyendo el guard de `CompanyId` nulo).
- `GeneratePayrollPeriodHandlerTests`: caso con bonificación fija, caso con bonificación %, caso mixto bono+descuento+adelanto (verificar `NetAmount` correcto), caso bonificación pendiente sin sueldo base configurado.
- `CancelLiquidationHandlerTests`: verificar que las bonificaciones vuelven a `Pending` al cancelar.

## Fuera de alcance / futuro (no bloquea esta implementación)

- Bonificación por % sobre ventas reales del período (requiere integrar `Sales`).
- Aplicación automática de presentismo en base a inasistencias.
- Aprobación en dos pasos (alta ≠ usuario que aprueba) — hoy ni adelantos ni descuentos lo tienen, se mantiene consistencia.
