# Notas de crédito — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Emitir y anular notas de crédito de cliente y de proveedor, que ajustan el saldo sin mover mercadería y quedan visibles en caja.

**Architecture:** Dos entidades espejo (`CustomerCreditNote` / `SupplierCreditNote`) que solo ORIGINAN crédito. La distribución FIFO, la imputación y la reversión ya existen y se reutilizan (`CustomerCreditApplicator`, `SupplierCreditApplicator`). Cada NC lleva un back-link propio (`CreditNoteId`) en la fila imputada para poder deshacer exactamente lo suyo.

**Tech Stack:** .NET 10 · Clean Architecture · Vertical Slice · MediatR · FluentValidation · EF Core / Npgsql · Angular 16 standalone

**Spec:** `docs/superpowers/specs/2026-09-05-notas-credito-design.md`

## Global Constraints

- La NC **no altera `ExpectedClosingAmount`**. Todos sus movimientos de caja son `CashMovementDirection.None`.
- La NC **baja `SaldoPendiente`, nunca sube `CobradoTotal`**. No entró dinero.
- `Reason` es **requerido**, máximo 250 caracteres. Es la única trazabilidad del ajuste.
- Las imputaciones FIFO de una NC llevan `CreditNoteId` seteado. **Nunca `Guid.Empty`.**
- `Result<T>` siempre; jamás `throw` para errores de negocio. Errores en `<Feature>Errors.cs`, `public static readonly`.
- Todo handler abre con `_currentUserService.EnsureAuthenticatedWithContext()`.
- Permisos nuevos van **dentro del bloque temático existente** de las 5 listas, nunca al final.
- Build backend siempre CON dependencias: `dotnet build eiti.Application/eiti.Application.csproj`.
- Build front obligatorio antes de dar por cerrada cualquier tarea de front: `cd C:/EiTeFront/eiti-front && ng build --configuration development`.
- Prefijos de código: `NCC-###` (cliente), `NCP-###` (proveedor). Por sucursal, `PadLeft(3, '0')`.

---

## File Structure

**Backend — dominio**
- `eiti.Domain/Customers/CustomerCreditNote.cs` — entidad NC de cliente
- `eiti.Domain/Suppliers/SupplierCreditNote.cs` — entidad NC de proveedor
- `eiti.Domain/Common/CreditNoteStatus.cs` — enum compartido Active/Cancelled
- `eiti.Domain/Sales/SaleCcPayment.cs` — suma `CreditNoteId`
- `eiti.Domain/Sales/Sale.cs` — `ApplyCustomerCredit` acepta `creditNoteId`; nuevo `RevertCreditNote`
- `eiti.Domain/Purchases/PurchasePayment.cs` — suma `CreditNoteId`
- `eiti.Domain/Cash/CashMovementType.cs` — 4 tipos nuevos
- `eiti.Domain/Cash/CashReferenceTypes.cs` — `CreditNote`
- `eiti.Domain/Cash/CashSession.cs` — 4 métodos `Register*` neutros

**Backend — aplicación**
- `Features/Customers/Commands/CreateCustomerCreditNote/` — slice completo
- `Features/Customers/Commands/CancelCustomerCreditNote/` — slice completo
- `Features/Suppliers/Commands/CreateSupplierCreditNote/` — slice completo
- `Features/Suppliers/Commands/CancelSupplierCreditNote/` — slice completo
- `Features/Customers/Common/CustomerCreditApplicator.cs` — acepta `creditNoteId`
- `Features/Purchases/Common/SupplierCreditApplicator.cs` — acepta `creditNoteId`
- `Abstractions/Repositories/ICustomerCreditNoteRepository.cs` · `ISupplierCreditNoteRepository.cs`
- `Abstractions/Repositories/ISaleRepository.cs` — `ListByCreditNoteIdAsync`
- `Abstractions/Repositories/IPurchaseRepository.cs` — `ListByCreditNoteIdAsync`

**Backend — infraestructura**
- `Persistence/Configurations/CustomerCreditNoteConfiguration.cs` · `SupplierCreditNoteConfiguration.cs`
- `Persistence/Repositories/CustomerCreditNoteRepository.cs` · `SupplierCreditNoteRepository.cs`
- `Persistence/ApplicationDbContext.cs` — 2 DbSets
- Migración `AddCreditNotes`

**Frontend**
- `core/models/credit-note.models.ts`
- `core/services/customer-account.service.ts` · `supplier-account.service.ts` — 2 métodos cada uno
- `features/clients/customer-account.component.*` — botón, modal, fila
- `features/purchases/supplier-account.component.*` — ídem
- `features/cash/cash.component.ts` — 4 etiquetas + badges

---

## Task 1: Entidad `CustomerCreditNote` y `CreditNoteStatus`

**Files:**
- Create: `eiti.Domain/Common/CreditNoteStatus.cs`
- Create: `eiti.Domain/Customers/CustomerCreditNote.cs`
- Test: `eiti.Tests/CustomerCreditNoteTests.cs`

**Interfaces:**
- Produces: `CreditNoteStatus { Active = 1, Cancelled = 2 }`; `CustomerCreditNote.Create(Guid companyId, Guid customerId, Guid branchId, string code, decimal amount, string reason, DateTime date, Guid? saleId, Guid createdByUserId) → CustomerCreditNote`; `.Cancel(Guid cancelledByUserId)`

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
using eiti.Domain.Common;
using eiti.Domain.Customers;
using FluentAssertions;
using Xunit;

namespace eiti.Tests;

public class CustomerCreditNoteTests
{
    private static CustomerCreditNote Sample(decimal amount = 50_000m, string reason = "Bonificación acordada") =>
        CustomerCreditNote.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "NCC-001",
            amount, reason, new DateTime(2026, 9, 5), null, Guid.NewGuid());

    [Fact]
    public void Create_NaceActiva_YRedondeaElImporte()
    {
        var note = CustomerCreditNote.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "NCC-001",
            50_000.456m, "Bonificación", new DateTime(2026, 9, 5), null, Guid.NewGuid());

        note.Status.Should().Be(CreditNoteStatus.Active);
        note.Amount.Should().Be(50_000.46m);
        note.CancelledAt.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RechazaImporteNoPositivo(decimal amount)
    {
        var act = () => Sample(amount: amount);
        act.Should().Throw<ArgumentException>();
    }

    // El motivo es la unica trazabilidad de un ajuste sin documento de origen.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RechazaMotivoVacio(string reason)
    {
        var act = () => Sample(reason: reason);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RechazaMotivoDemasiadoLargo()
    {
        var act = () => Sample(reason: new string('x', 251));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_MarcaCanceladaYGuardaQuienYCuando()
    {
        var note = Sample();
        var userId = Guid.NewGuid();

        note.Cancel(userId);

        note.Status.Should().Be(CreditNoteStatus.Cancelled);
        note.CancelledByUserId.Should().Be(userId);
        note.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_DosVeces_Lanza()
    {
        var note = Sample();
        note.Cancel(Guid.NewGuid());

        var act = () => note.Cancel(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~CustomerCreditNoteTests"`
Expected: FAIL — `CustomerCreditNote` no existe.

- [ ] **Step 3: Crear el enum**

```csharp
namespace eiti.Domain.Common;

public enum CreditNoteStatus
{
    Active = 1,
    Cancelled = 2
}
```

- [ ] **Step 4: Crear la entidad**

```csharp
using eiti.Domain.Common;

namespace eiti.Domain.Customers;

// Nota de crédito emitida al cliente: ajusta el saldo sin mover mercadería (bonificación
// posterior, error de facturación, diferencia de precio acordada). Origina saldo a favor que
// se imputa FIFO a las ventas CC pendientes, igual que un cobro — pero sin dinero de por medio.
// Espejo de SupplierCreditNote.
public sealed class CustomerCreditNote
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid BranchId { get; private set; }

    public string Code { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime Date { get; private set; }

    // Venta asociada, opcional: la primera pregunta de cualquier contador es "¿por qué factura es?".
    public Guid? SaleId { get; private set; }

    public CreditNoteStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledByUserId { get; private set; }

    private CustomerCreditNote()
    {
    }

    private CustomerCreditNote(
        Guid id,
        Guid companyId,
        Guid customerId,
        Guid branchId,
        string code,
        decimal amount,
        string reason,
        DateTime date,
        Guid? saleId,
        Guid createdByUserId)
    {
        if (amount <= 0)
            throw new ArgumentException("Credit note amount must be greater than zero.", nameof(amount));

        Id = id;
        CompanyId = companyId;
        CustomerId = customerId;
        BranchId = branchId;
        Code = NormalizeRequired(code, 20, nameof(code));
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Reason = NormalizeRequired(reason, 250, nameof(reason));
        Date = date;
        SaleId = saleId;
        Status = CreditNoteStatus.Active;
        CreatedAt = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
    }

    public static CustomerCreditNote Create(
        Guid companyId,
        Guid customerId,
        Guid branchId,
        string code,
        decimal amount,
        string reason,
        DateTime date,
        Guid? saleId,
        Guid createdByUserId)
    {
        return new CustomerCreditNote(
            Guid.NewGuid(), companyId, customerId, branchId, code, amount, reason, date, saleId, createdByUserId);
    }

    public void Cancel(Guid cancelledByUserId)
    {
        if (Status == CreditNoteStatus.Cancelled)
            throw new InvalidOperationException("Credit note is already cancelled.");

        Status = CreditNoteStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancelledByUserId = cancelledByUserId;
    }

    private static string NormalizeRequired(string value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException($"{paramName} cannot exceed {maxLength} characters.", paramName);

        return normalized;
    }
}
```

- [ ] **Step 5: Correr y verificar que pasa**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~CustomerCreditNoteTests"`
Expected: PASS (7 tests)

- [ ] **Step 6: Commit**

```bash
git add eiti.Domain/Common/CreditNoteStatus.cs eiti.Domain/Customers/CustomerCreditNote.cs eiti.Tests/CustomerCreditNoteTests.cs
git commit -m "feat(dominio): entidad CustomerCreditNote con motivo obligatorio"
```

---

## Task 2: Back-link `CreditNoteId` en la imputación de venta

Esta es la tarea crítica del plan. Sin back-link propio, las imputaciones de la NC nacen con `Guid.Empty` y anular la NC no puede deshacerlas — el patrón "cancelar el padre deja hijos activos" que ya nos costó tres bugs.

**Files:**
- Modify: `eiti.Domain/Sales/SaleCcPayment.cs`
- Modify: `eiti.Domain/Sales/Sale.cs:328` (`ApplyCustomerCredit`), y nuevo `RevertCreditNote`
- Test: `eiti.Tests/SaleCreditNoteImputationTests.cs`

**Interfaces:**
- Consumes: `CustomerCreditNote` (Task 1)
- Produces: `SaleCcPayment.CreditNoteId` (`Guid?`); `Sale.ApplyCustomerCredit(decimal amount, DateTime date, Guid customerPaymentId, string? notes, Guid? creditNoteId = null) → bool`; `Sale.RevertCreditNote(Guid creditNoteId) → bool`

- [ ] **Step 1: Escribir el test que falla**

El test central: una venta con imputaciones de DOS orígenes distintos; revertir una no toca la otra.

```csharp
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using FluentAssertions;
using Xunit;

namespace eiti.Tests;

public class SaleCreditNoteImputationTests
{
    private static Sale CcSale(decimal total) =>
        Sale.CreateCc(
            CompanyId.New(),
            Domain.Branches.BranchId.New(),
            CustomerId.New(),
            [SaleDetail.Create(Domain.Products.ProductId.New(), 1, total)]);

    [Fact]
    public void ApplyCustomerCredit_ConCreditNoteId_DejaElBackLinkEnLaFila()
    {
        var sale = CcSale(100_000m);
        var noteId = Guid.NewGuid();

        sale.ApplyCustomerCredit(30_000m, DateTime.UtcNow, Guid.Empty, "NC", creditNoteId: noteId);

        var row = sale.CcPayments.Single();
        row.CreditNoteId.Should().Be(noteId);
        row.CustomerPaymentId.Should().Be(Guid.Empty);
        row.Method.Should().Be(SalePaymentMethod.CustomerCredit);
    }

    // El corazon del diseño: cada origen deshace SOLO lo suyo.
    [Fact]
    public void RevertCreditNote_NoTocaLasImputacionesDeOtrosOrigenes()
    {
        var sale = CcSale(100_000m);
        var noteId = Guid.NewGuid();
        var otherNoteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        sale.ApplyCustomerCredit(20_000m, DateTime.UtcNow, Guid.Empty, "NC 1", creditNoteId: noteId);
        sale.ApplyCustomerCredit(30_000m, DateTime.UtcNow, Guid.Empty, "NC 2", creditNoteId: otherNoteId);
        sale.ApplyCustomerCredit(10_000m, DateTime.UtcNow, paymentId, "Cobro");

        sale.RevertCreditNote(noteId);

        var active = sale.CcPayments
            .Where(p => p.Status == SaleCcPaymentStatus.Active)
            .ToList();

        active.Should().HaveCount(2);
        active.Should().NotContain(p => p.CreditNoteId == noteId);
        active.Should().Contain(p => p.CreditNoteId == otherNoteId);
        active.Should().Contain(p => p.CustomerPaymentId == paymentId);
    }

    [Fact]
    public void RevertCreditNote_SinFilasDeEsaNota_DevuelveFalse()
    {
        var sale = CcSale(100_000m);
        sale.ApplyCustomerCredit(20_000m, DateTime.UtcNow, Guid.Empty, "NC", creditNoteId: Guid.NewGuid());

        sale.RevertCreditNote(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void RevertCreditNote_DevuelveLaVentaAPendiente_SiEstabaPaga()
    {
        var sale = CcSale(50_000m);
        var noteId = Guid.NewGuid();

        var becamePaid = sale.ApplyCustomerCredit(50_000m, DateTime.UtcNow, Guid.Empty, "NC", creditNoteId: noteId);
        becamePaid.Should().BeTrue();
        sale.SaleStatus.Should().Be(SaleStatus.Paid);

        var revertedFromPaid = sale.RevertCreditNote(noteId);

        revertedFromPaid.Should().BeTrue();
        sale.SaleStatus.Should().Be(SaleStatus.OnHold);
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~SaleCreditNoteImputationTests"`
Expected: FAIL — `CreditNoteId` y `RevertCreditNote` no existen.

- [ ] **Step 3: Sumar `CreditNoteId` a `SaleCcPayment`**

En `eiti.Domain/Sales/SaleCcPayment.cs`, debajo de la propiedad `CustomerPaymentId`:

```csharp
    // Back-link con la nota de crédito que generó esta imputación. Hermano de CustomerPaymentId:
    // solo uno de los dos se setea. Sin esto, anular una NC no podría deshacer exactamente lo suyo.
    public Guid? CreditNoteId { get; private set; }
```

Sumar el parámetro al constructor privado y al factory, ambos al final y opcional:

```csharp
        Guid? customerPaymentId = null,
        Guid? creditNoteId = null)
```

Y en el cuerpo del constructor, junto a `CustomerPaymentId = customerPaymentId;`:

```csharp
        CreditNoteId = creditNoteId;
```

En el factory `Create`, pasar `creditNoteId` al constructor.

- [ ] **Step 4: Extender `Sale.ApplyCustomerCredit` y agregar `RevertCreditNote`**

En `eiti.Domain/Sales/Sale.cs`, cambiar la firma de `ApplyCustomerCredit` (L328) sumando un parámetro opcional al final:

```csharp
    public bool ApplyCustomerCredit(decimal amount, DateTime date, Guid customerPaymentId, string? notes, Guid? creditNoteId = null)
```

y pasarlo al `SaleCcPayment.Create(...)` de adentro:

```csharp
        var payment = SaleCcPayment.Create(
            Id,
            SalePaymentMethod.CustomerCredit,
            amount,
            date,
            notes,
            groupId: null,
            customerPaymentId: customerPaymentId,
            creditNoteId: creditNoteId);
```

Agregar `RevertCreditNote` justo debajo de `RevertCustomerCredit`, copiando su cuerpo y cambiando solo el predicado del filtro:

```csharp
    // Espejo de RevertCustomerCredit, filtrando por CreditNoteId. Devuelve true si la venta
    // estaba Paid y volvió a pendiente (el caller tiene que devolver el stock a reservado).
    public bool RevertCreditNote(Guid creditNoteId)
    {
        if (!IsCuentaCorriente)
        {
            throw new InvalidOperationException("CC payments can only be cancelled on Cuenta Corriente sales.");
        }

        var rows = _ccPayments
            .Where(p => p.CreditNoteId == creditNoteId && p.Status == SaleCcPaymentStatus.Active)
            .ToList();

        if (rows.Count == 0)
        {
            return false;
        }

        var wasPaid = SaleStatus == SaleStatus.Paid;

        foreach (var row in rows)
        {
            row.Cancel();
        }

        if (wasPaid && NormalizeAmount(CcSettledTotal) < NormalizeAmount(TotalAmount))
        {
            RevertToOnHoldFromCc();
            return true;
        }

        return false;
    }
```

- [ ] **Step 5: Correr y verificar que pasa**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~SaleCreditNoteImputationTests"`
Expected: PASS (4 tests)

- [ ] **Step 6: Correr toda la suite (no romper nada existente)**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj`
Expected: PASS, sin regresiones. `ApplyCustomerCredit` sumó un parámetro opcional, así que los llamadores existentes compilan sin cambios.

- [ ] **Step 7: Commit**

```bash
git add eiti.Domain/Sales/SaleCcPayment.cs eiti.Domain/Sales/Sale.cs eiti.Tests/SaleCreditNoteImputationTests.cs
git commit -m "feat(dominio): back-link CreditNoteId en las imputaciones de venta"
```

---

## Task 3: Caja — tipos de movimiento neutros

**Files:**
- Modify: `eiti.Domain/Cash/CashMovementType.cs`
- Modify: `eiti.Domain/Cash/CashReferenceTypes.cs`
- Modify: `eiti.Domain/Cash/CashSession.cs`
- Test: `eiti.Tests/CashSessionCreditNoteTests.cs`

**Interfaces:**
- Produces: `CashSession.RegisterCustomerCreditNote(decimal amount, Guid creditNoteId, string code, UserId userId)`, `.RegisterCustomerCreditNoteCancellation(...)`, `.RegisterSupplierCreditNote(...)`, `.RegisterSupplierCreditNoteCancellation(...)` — las cuatro con la misma firma.

- [ ] **Step 1: Escribir el test que falla**

El test que importa: el arqueo no se mueve.

```csharp
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using FluentAssertions;
using Xunit;

namespace eiti.Tests;

public class CashSessionCreditNoteTests
{
    private static CashSession OpenSession()
    {
        var session = CashSession.Open(
            CompanyId.New(),
            Domain.Branches.BranchId.New(),
            CashDrawerId.New(),
            100_000m,
            UserId.New());
        return session;
    }

    // LA invariante del feature: una NC se ve en caja pero no mueve el efectivo esperado.
    [Fact]
    public void NotaDeCredito_NoAlteraElArqueo()
    {
        var session = OpenSession();
        var expectedBefore = session.ExpectedClosingAmount;

        session.RegisterCustomerCreditNote(50_000m, Guid.NewGuid(), "NCC-001", UserId.New());

        session.ExpectedClosingAmount.Should().Be(expectedBefore);
    }

    [Fact]
    public void NotaDeCredito_QuedaVisibleEnLosMovimientos()
    {
        var session = OpenSession();
        var noteId = Guid.NewGuid();

        session.RegisterCustomerCreditNote(50_000m, noteId, "NCC-001", UserId.New());

        var movement = session.Movements.Single(m => m.Type == CashMovementType.CustomerCreditNote);
        movement.Direction.Should().Be(CashMovementDirection.None);
        movement.Amount.Should().Be(50_000m);
        movement.ReferenceType.Should().Be(CashReferenceTypes.CreditNote);
        movement.ReferenceId.Should().Be(noteId);
        movement.Description.Should().Contain("NCC-001");
    }

    [Fact]
    public void AnulacionDeNotaDeCredito_TampocoAlteraElArqueo()
    {
        var session = OpenSession();
        var expectedBefore = session.ExpectedClosingAmount;

        session.RegisterCustomerCreditNoteCancellation(50_000m, Guid.NewGuid(), "NCC-001", UserId.New());

        session.ExpectedClosingAmount.Should().Be(expectedBefore);
        session.Movements.Should().Contain(m => m.Type == CashMovementType.CustomerCreditNoteCancellation);
    }

    [Fact]
    public void NotaDeCreditoDeProveedor_MismoTratamiento()
    {
        var session = OpenSession();
        var expectedBefore = session.ExpectedClosingAmount;

        session.RegisterSupplierCreditNote(30_000m, Guid.NewGuid(), "NCP-001", UserId.New());
        session.RegisterSupplierCreditNoteCancellation(30_000m, Guid.NewGuid(), "NCP-001", UserId.New());

        session.ExpectedClosingAmount.Should().Be(expectedBefore);
        session.Movements.Should().Contain(m => m.Type == CashMovementType.SupplierCreditNote);
        session.Movements.Should().Contain(m => m.Type == CashMovementType.SupplierCreditNoteCancellation);
    }
}
```

> Si la firma real de `CashSession.Open` difiere, leer `eiti.Domain/Cash/CashSession.cs` y ajustar el helper `OpenSession()` — el resto del test no cambia.

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~CashSessionCreditNoteTests"`
Expected: FAIL — los tipos y métodos no existen.

- [ ] **Step 3: Sumar los tipos**

En `eiti.Domain/Cash/CashMovementType.cs`, al final del enum (los valores son secuenciales; el último actual es `PayrollAdvanceExpenseCancellation = 18`):

```csharp
    CustomerCreditNote = 19,
    CustomerCreditNoteCancellation = 20,
    SupplierCreditNote = 21,
    SupplierCreditNoteCancellation = 22
```

En `eiti.Domain/Cash/CashReferenceTypes.cs`, al final de la clase:

```csharp
    public const string CreditNote = "CreditNote";
```

- [ ] **Step 4: Sumar los cuatro métodos a `CashSession`**

Ubicarlos después de `RegisterCustomerPaymentCancellation` (L303), que es su vecino temático.

```csharp
    // Notas de crédito: se ven en la sesión del día pero NO mueven efectivo, así que van con
    // dirección None y ExpectedClosingAmount las suma como 0. Mismo tratamiento que un pago CC
    // con cheque (RegisterCcNonCashIncome).
    public void RegisterCustomerCreditNote(decimal amount, Guid creditNoteId, string code, UserId createdByUserId)
    {
        EnsureOpen();

        AddMovement(
            CashMovementType.CustomerCreditNote,
            CashMovementDirection.None,
            amount,
            CashReferenceTypes.CreditNote,
            creditNoteId,
            $"Nota de crédito a cliente {code}",
            createdByUserId);
    }

    public void RegisterCustomerCreditNoteCancellation(decimal amount, Guid creditNoteId, string code, UserId createdByUserId)
    {
        EnsureOpen();

        AddMovement(
            CashMovementType.CustomerCreditNoteCancellation,
            CashMovementDirection.None,
            amount,
            CashReferenceTypes.CreditNote,
            creditNoteId,
            $"Anulación de nota de crédito a cliente {code}",
            createdByUserId);
    }

    public void RegisterSupplierCreditNote(decimal amount, Guid creditNoteId, string code, UserId createdByUserId)
    {
        EnsureOpen();

        AddMovement(
            CashMovementType.SupplierCreditNote,
            CashMovementDirection.None,
            amount,
            CashReferenceTypes.CreditNote,
            creditNoteId,
            $"Nota de crédito de proveedor {code}",
            createdByUserId);
    }

    public void RegisterSupplierCreditNoteCancellation(decimal amount, Guid creditNoteId, string code, UserId createdByUserId)
    {
        EnsureOpen();

        AddMovement(
            CashMovementType.SupplierCreditNoteCancellation,
            CashMovementDirection.None,
            amount,
            CashReferenceTypes.CreditNote,
            creditNoteId,
            $"Anulación de nota de crédito de proveedor {code}",
            createdByUserId);
    }
```

- [ ] **Step 5: Correr y verificar que pasa**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~CashSessionCreditNoteTests"`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add eiti.Domain/Cash/
git add eiti.Tests/CashSessionCreditNoteTests.cs
git commit -m "feat(caja): movimientos neutros para notas de credito"
```

---

## Task 4: Entidad `SupplierCreditNote` y back-link en compras

**Files:**
- Create: `eiti.Domain/Suppliers/SupplierCreditNote.cs`
- Modify: `eiti.Domain/Purchases/PurchasePayment.cs`
- Modify: `eiti.Domain/Purchases/Purchase.cs`
- Test: `eiti.Tests/SupplierCreditNoteTests.cs`

**Interfaces:**
- Consumes: `CreditNoteStatus` (Task 1)
- Produces: `SupplierCreditNote.Create(Guid companyId, Guid supplierId, Guid branchId, string code, decimal amount, string reason, DateTime date, Guid? purchaseId, Guid createdByUserId)`; `.Cancel(Guid cancelledByUserId)`; `PurchasePayment.CreditNoteId` (`Guid?`); `Purchase.RevertCreditNote(Guid creditNoteId) → bool`

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
using eiti.Domain.Common;
using eiti.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eiti.Tests;

public class SupplierCreditNoteTests
{
    private static SupplierCreditNote Sample(decimal amount = 50_000m, string reason = "Bonificación del proveedor") =>
        SupplierCreditNote.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "NCP-001",
            amount, reason, new DateTime(2026, 9, 5), null, Guid.NewGuid());

    [Fact]
    public void Create_NaceActiva()
    {
        var note = Sample();
        note.Status.Should().Be(CreditNoteStatus.Active);
        note.Amount.Should().Be(50_000m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RechazaImporteNoPositivo(decimal amount)
    {
        var act = () => Sample(amount: amount);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RechazaMotivoVacio()
    {
        var act = () => Sample(reason: "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_DosVeces_Lanza()
    {
        var note = Sample();
        note.Cancel(Guid.NewGuid());

        var act = () => note.Cancel(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~SupplierCreditNoteTests"`
Expected: FAIL — `SupplierCreditNote` no existe.

- [ ] **Step 3: Crear `SupplierCreditNote`**

Copia exacta de `eiti.Domain/Customers/CustomerCreditNote.cs` (Task 1, Step 4) con estos cambios y nada más:

- `namespace eiti.Domain.Suppliers;`
- nombre de clase `SupplierCreditNote`
- `CustomerId` → `SupplierId` (propiedad, parámetros de constructor y factory)
- `SaleId` → `PurchaseId` (propiedad, parámetros de constructor y factory)
- comentario de cabecera: `// Nota de crédito recibida del proveedor: ajusta el saldo sin mover mercadería. Espejo de CustomerCreditNote.`

Todo lo demás — validaciones, `NormalizeRequired`, `Cancel`, redondeo — idéntico.

- [ ] **Step 4: Sumar `CreditNoteId` a `PurchasePayment`**

En `eiti.Domain/Purchases/PurchasePayment.cs`, debajo de `SupplierPaymentId`:

```csharp
    // Si la fila es una imputación FIFO generada por una nota de crédito, apunta a esa NC.
    // Hermano de SupplierPaymentId: solo uno de los dos se setea.
    public Guid? CreditNoteId { get; private set; }
```

Sumar `Guid? creditNoteId = null` como último parámetro del constructor privado y del factory `Create`, asignando `CreditNoteId = creditNoteId;` junto a `SupplierPaymentId = supplierPaymentId;`.

- [ ] **Step 5: Agregar `Purchase.RevertCreditNote`**

`SupplierPaymentReversal` hoy cancela las filas con `purchase.CancelPayment(row.Id)` desde afuera. Para la NC se encapsula en el agregado, que es donde va la invariante:

```csharp
    // Cancela las imputaciones que generó una nota de crédito y recalcula el estado.
    // Devuelve el total desimputado, para que el caller sepa cuánto crédito revertir.
    public decimal RevertCreditNote(Guid creditNoteId)
    {
        var rows = _payments
            .Where(p => p.CreditNoteId == creditNoteId && p.Status == PurchasePaymentStatus.Active)
            .ToList();

        var total = 0m;
        foreach (var row in rows)
        {
            total += row.Amount;
            CancelPayment(row.Id);
        }

        return total;
    }
```

> `CancelPayment` ya existe en `Purchase` y ya recalcula el estado; no duplicar esa lógica.

> **Ojo con la asimetría de retorno, es intencional.** `Sale.RevertCreditNote` devuelve `bool`
> (si la venta volvió de Paid a pendiente, para que el caller devuelva el stock a reservado);
> `Purchase.RevertCreditNote` devuelve `decimal` (el total desimputado). Las compras no reservan
> stock, y del lado venta el total se calcula aparte antes de revertir. No unificarlas.

- [ ] **Step 6: Correr y verificar que pasa**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~SupplierCreditNoteTests"`
Expected: PASS (5 tests)

- [ ] **Step 7: Commit**

```bash
git add eiti.Domain/Suppliers/SupplierCreditNote.cs eiti.Domain/Purchases/ eiti.Tests/SupplierCreditNoteTests.cs
git commit -m "feat(dominio): entidad SupplierCreditNote y back-link en compras"
```

---

## Task 5: Persistencia — configuraciones, repositorios y migración

**Files:**
- Create: `eiti.Infrastructure/Persistence/Configurations/CustomerCreditNoteConfiguration.cs`
- Create: `eiti.Infrastructure/Persistence/Configurations/SupplierCreditNoteConfiguration.cs`
- Create: `eiti.Application/Abstractions/Repositories/ICustomerCreditNoteRepository.cs`
- Create: `eiti.Application/Abstractions/Repositories/ISupplierCreditNoteRepository.cs`
- Create: `eiti.Infrastructure/Persistence/Repositories/CustomerCreditNoteRepository.cs`
- Create: `eiti.Infrastructure/Persistence/Repositories/SupplierCreditNoteRepository.cs`
- Modify: `eiti.Infrastructure/Persistence/ApplicationDbContext.cs`
- Modify: `eiti.Infrastructure/Persistence/Configurations/SaleCcPaymentConfiguration.cs`
- Modify: `eiti.Infrastructure/Persistence/Configurations/PurchasePaymentConfiguration.cs`
- Modify: `eiti.Application/Abstractions/Repositories/ISaleRepository.cs` · `IPurchaseRepository.cs`
- Modify: `eiti.Infrastructure/Persistence/Repositories/SaleRepository.cs` · `PurchaseRepository.cs`
- Modify: registro de DI (mismo archivo donde se registra `CustomerPaymentRepository`)

**Interfaces:**
- Consumes: `CustomerCreditNote`, `SupplierCreditNote`, `SaleCcPayment.CreditNoteId`, `PurchasePayment.CreditNoteId`
- Produces:
  - `ICustomerCreditNoteRepository`: `GetByIdAsync(Guid id, Guid companyId, CancellationToken)`, `ListByCustomerAsync(Guid companyId, Guid customerId, CancellationToken)`, `CountByBranchAsync(Guid companyId, Guid branchId, CancellationToken)`, `AddAsync(CustomerCreditNote, CancellationToken)`
  - `ISupplierCreditNoteRepository`: espejo con `ListBySupplierAsync`
  - `ISaleRepository.ListByCreditNoteIdAsync(CompanyId companyId, Guid creditNoteId, CancellationToken) → Task<IReadOnlyList<Sale>>`
  - `IPurchaseRepository.ListByCreditNoteIdAsync(Guid companyId, Guid creditNoteId, CancellationToken) → Task<List<Purchase>>`

- [ ] **Step 1: Configuración EF de `CustomerCreditNote`**

Sigue el molde de `CustomerPaymentConfiguration.cs`:

```csharp
using eiti.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class CustomerCreditNoteConfiguration : IEntityTypeConfiguration<CustomerCreditNote>
{
    public void Configure(EntityTypeBuilder<CustomerCreditNote> builder)
    {
        builder.ToTable("CustomerCreditNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).IsRequired().ValueGeneratedNever();
        builder.Property(n => n.CompanyId).IsRequired();
        builder.Property(n => n.CustomerId).IsRequired();
        builder.Property(n => n.BranchId).IsRequired();
        builder.Property(n => n.Code).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(n => n.Reason).HasMaxLength(250).IsRequired();
        builder.Property(n => n.Date).IsRequired();
        builder.Property(n => n.SaleId).IsRequired(false);
        builder.Property(n => n.Status).HasConversion<int>().IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.CreatedByUserId).IsRequired();
        builder.Property(n => n.CancelledAt).IsRequired(false);
        builder.Property(n => n.CancelledByUserId).IsRequired(false);

        builder.HasIndex(n => new { n.CompanyId, n.CustomerId });
        builder.HasIndex(n => n.Status);

        // No se modela FK navegable a Customer: su PK es el value object CustomerId y aquí es Guid
        // (mismo patrón que CustomerPaymentConfiguration).
    }
}
```

- [ ] **Step 2: Configuración EF de `SupplierCreditNote`**

Idéntica, con `ToTable("SupplierCreditNotes")`, `SupplierId` en lugar de `CustomerId`, `PurchaseId` en lugar de `SaleId`, e índice `new { n.CompanyId, n.SupplierId }`.

- [ ] **Step 3: Columnas nuevas en las configuraciones existentes**

En `SaleCcPaymentConfiguration.cs`, junto a la línea de `CustomerPaymentId`:

```csharp
        builder.Property(p => p.CreditNoteId).IsRequired(false);
        builder.HasIndex(p => p.CreditNoteId);
```

En `PurchasePaymentConfiguration.cs`, junto a la de `SupplierPaymentId`:

```csharp
        builder.Property(p => p.CreditNoteId).IsRequired(false);
        builder.HasIndex(p => p.CreditNoteId);
```

> El índice no es opcional: `ListByCreditNoteIdAsync` filtra por esa columna en cada anulación.

- [ ] **Step 4: DbSets**

En `ApplicationDbContext.cs`, junto a `CustomerPayments` y `SupplierPayments`:

```csharp
    public DbSet<CustomerCreditNote> CustomerCreditNotes => Set<CustomerCreditNote>();
    public DbSet<SupplierCreditNote> SupplierCreditNotes => Set<SupplierCreditNote>();
```

- [ ] **Step 5: Interfaces de repositorio**

```csharp
using eiti.Domain.Customers;

namespace eiti.Application.Abstractions.Repositories;

public interface ICustomerCreditNoteRepository
{
    Task<CustomerCreditNote?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    Task<List<CustomerCreditNote>> ListByCustomerAsync(Guid companyId, Guid customerId, CancellationToken ct = default);

    // Para numerar NCC-### por sucursal. Cuenta todas, incluidas las anuladas, igual que
    // CountByBranchAsync de ventas: el número emitido no se reutiliza.
    Task<int> CountByBranchAsync(Guid companyId, Guid branchId, CancellationToken ct = default);

    Task AddAsync(CustomerCreditNote note, CancellationToken ct = default);
}
```

`ISupplierCreditNoteRepository` es el espejo: `SupplierCreditNote`, `ListBySupplierAsync(Guid companyId, Guid supplierId, ...)`, mismo `CountByBranchAsync` y `AddAsync`.

- [ ] **Step 6: Implementaciones**

Molde de `CustomerPaymentRepository.cs`:

```csharp
using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class CustomerCreditNoteRepository : ICustomerCreditNoteRepository
{
    private readonly ApplicationDbContext _db;

    public CustomerCreditNoteRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerCreditNote?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await _db.CustomerCreditNotes
            .FirstOrDefaultAsync(n => n.Id == id && n.CompanyId == companyId, ct);
    }

    public async Task<List<CustomerCreditNote>> ListByCustomerAsync(Guid companyId, Guid customerId, CancellationToken ct = default)
    {
        return await _db.CustomerCreditNotes
            .Where(n => n.CompanyId == companyId && n.CustomerId == customerId)
            .OrderByDescending(n => n.Date)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> CountByBranchAsync(Guid companyId, Guid branchId, CancellationToken ct = default)
    {
        return await _db.CustomerCreditNotes
            .CountAsync(n => n.CompanyId == companyId && n.BranchId == branchId, ct);
    }

    public async Task AddAsync(CustomerCreditNote note, CancellationToken ct = default)
    {
        await _db.CustomerCreditNotes.AddAsync(note, ct);
    }
}
```

`SupplierCreditNoteRepository` es el espejo.

- [ ] **Step 7: Consultas por back-link**

En `ISaleRepository.cs`, junto a `ListByCustomerPaymentIdAsync` (L135):

```csharp
    // Ventas con imputaciones activas de una nota de crédito, para poder deshacerlas al anularla.
    Task<IReadOnlyList<Sale>> ListByCreditNoteIdAsync(
        CompanyId companyId,
        Guid creditNoteId,
        CancellationToken cancellationToken = default);
```

En `SaleRepository.cs`, copiar el cuerpo de `ListByCustomerPaymentIdAsync` cambiando el predicado a `p.CreditNoteId == creditNoteId`. Debe traer `.Include(s => s.CcPayments)` y `.Include(s => s.Details)` igual que el original (el caller confirma stock con `Details`).

En `IPurchaseRepository.cs`, junto a `ListBySupplierPaymentIdAsync` (L53):

```csharp
    Task<List<Purchase>> ListByCreditNoteIdAsync(
        Guid companyId,
        Guid creditNoteId,
        CancellationToken cancellationToken = default);
```

En `PurchaseRepository.cs`, mismo trasplante desde `ListBySupplierPaymentIdAsync` (L118) con predicado `p.CreditNoteId == creditNoteId`.

- [ ] **Step 8: Registrar en DI**

En el archivo donde se registran los repositorios (buscar con `grep -rn "ICustomerPaymentRepository" eiti.Infrastructure --include=*.cs | grep -i "AddScoped"`), sumar en el mismo bloque:

```csharp
        services.AddScoped<ICustomerCreditNoteRepository, CustomerCreditNoteRepository>();
        services.AddScoped<ISupplierCreditNoteRepository, SupplierCreditNoteRepository>();
```

- [ ] **Step 9: Generar la migración**

Desde `eiti.Infrastructure` directamente (la API puede tener las DLL bloqueadas):

```bash
cd C:/Eiti/eiti/eiti.Infrastructure
dotnet ef migrations add AddCreditNotes
```

Revisar el archivo generado: debe crear `CustomerCreditNotes` y `SupplierCreditNotes` y agregar `CreditNoteId` a `SaleCcPayments` y `PurchasePayments`, con sus índices. **No debe** contener ningún `DropColumn` ni `AlterColumn` sobre tablas existentes.

- [ ] **Step 10: Build y suite completa**

```bash
cd C:/Eiti/eiti
dotnet build eiti.Application/eiti.Application.csproj
dotnet test eiti.Tests/eiti.Tests.csproj
```
Expected: 0 errores, suite en verde.

- [ ] **Step 11: Commit**

```bash
git add eiti.Infrastructure/ eiti.Application/Abstractions/Repositories/
git commit -m "feat(persistencia): tablas de notas de credito y back-links"
```

---

## Task 6: Permisos

**Files:**
- Modify: `eiti.Application/Common/Authorization/PermissionCodes.cs`
- Modify: `eiti.Application/Common/Authorization/PermissionCatalog.cs`
- Modify: `eiti.Application/Common/Authorization/RoleCatalog.cs`
- Modify: `C:/EiTeFront/eiti-front/src/app/core/models/permission.models.ts`

**Interfaces:**
- Produces: `PermissionCodes.SalesCreditNoteCreate` = `"sales.credit_note.create"`, `.SalesCreditNoteCancel` = `"sales.credit_note.cancel"`, `.PurchasesCreditNoteCreate` = `"purchases.credit_note.create"`, `.PurchasesCreditNoteCancel` = `"purchases.credit_note.cancel"`

> **Leer la lección de `.claude/rules/lessons.md` del 2026-08-03 antes de editar.** Cada constante va DENTRO de su bloque temático, no al final del archivo. Leer la lista entera y ubicar el grupo antes de anclar el `Edit`.

- [ ] **Step 1: `PermissionCodes.cs`**

En el bloque de ventas (junto a `SalesPay`, ~L9):

```csharp
    public const string SalesCreditNoteCreate = "sales.credit_note.create";
    public const string SalesCreditNoteCancel = "sales.credit_note.cancel";
```

En el bloque de compras (junto a `PurchasesPay`, ~L77):

```csharp
    public const string PurchasesCreditNoteCreate = "purchases.credit_note.create";
    public const string PurchasesCreditNoteCancel = "purchases.credit_note.cancel";
```

- [ ] **Step 2: `PermissionCatalog.All`**

Sumar las cuatro entradas en sus bloques temáticos. **Chequear el prefijo de las etiquetas vecinas antes de escribir** (si las de ventas dicen `"Ventas: ..."`, seguir ese prefijo exacto). Ejemplo, ajustando al prefijo real que se lea en el archivo:

```csharp
        new(PermissionCodes.SalesCreditNoteCreate, "Ventas: emitir nota de crédito"),
        new(PermissionCodes.SalesCreditNoteCancel, "Ventas: anular nota de crédito"),
```

- [ ] **Step 3: `RoleCatalog.cs`**

Asignar los cuatro a los mismos roles que ya tienen `SalesPay` / `PurchasesPay`, en la misma posición de cada lista.

- [ ] **Step 4: Front `permission.models.ts`**

Sumar al mapa `PermissionCodes` (camelCase: `salesCreditNoteCreate`, `salesCreditNoteCancel`, `purchasesCreditNoteCreate`, `purchasesCreditNoteCancel`) y al array `PermissionCatalog`, en sus bloques temáticos y con el prefijo de etiqueta de sus vecinos.

- [ ] **Step 5: Verificar que las cinco listas coinciden**

```bash
cd C:/Eiti/eiti
grep -c "credit_note" eiti.Application/Common/Authorization/PermissionCodes.cs eiti.Application/Common/Authorization/PermissionCatalog.cs
grep -c "CreditNote" eiti.Application/Common/Authorization/RoleCatalog.cs
grep -c "credit_note" C:/EiTeFront/eiti-front/src/app/core/models/permission.models.ts
```
Expected: 4 en `PermissionCodes`, 4 en `PermissionCatalog`, ≥4 en `RoleCatalog`, 4 en el front.

> Si falta en `PermissionCatalog.All`, asignar el permiso a un perfil de acceso falla con "One or more selected permissions are invalid". **Reiniciar la API** después de este cambio: es un set estático en memoria.

- [ ] **Step 6: Commit**

```bash
git add eiti.Application/Common/Authorization/
git commit -m "feat(permisos): codigos para emitir y anular notas de credito"
```

---

## Task 7: Emitir nota de crédito de cliente

**Files:**
- Create: `eiti.Application/Features/Customers/Commands/CreateCustomerCreditNote/CreateCustomerCreditNoteCommand.cs`
- Create: `.../CreateCustomerCreditNoteHandler.cs`
- Create: `.../CreateCustomerCreditNoteValidator.cs`
- Create: `.../CreateCustomerCreditNoteResponse.cs`
- Create: `.../CreateCustomerCreditNoteErrors.cs`
- Modify: `eiti.Application/Features/Customers/Common/CustomerCreditApplicator.cs`
- Modify: `eiti.Api/Controllers/CustomersController.cs`
- Test: `eiti.Tests/CreateCustomerCreditNoteHandlerTests.cs`

**Interfaces:**
- Consumes: `CustomerCreditNote.Create`, `Sale.ApplyCustomerCredit(..., creditNoteId)`, `CashSession.RegisterCustomerCreditNote`, `ICustomerCreditNoteRepository`, `PermissionCodes.SalesCreditNoteCreate`
- Produces: `CreateCustomerCreditNoteResponse(Guid Id, string Code, decimal Amount, decimal CustomerCreditBalance, IReadOnlyList<CustomerPaymentImputacion> Imputaciones, decimal Sobrante)`

- [ ] **Step 1: Extender el applicator**

En `CustomerCreditApplicator.ApplyToPendingCcSalesAsync`, sumar un parámetro al final:

```csharp
        Guid? customerPaymentId = null,
        Guid? creditNoteId = null)
```

y pasarlo en la llamada de adentro:

```csharp
            var becamePaid = sale.ApplyCustomerCredit(
                applied,
                DateTime.UtcNow,
                customerPaymentId ?? Guid.Empty,
                "Saldo a favor aplicado automáticamente",
                creditNoteId);
```

- [ ] **Step 2: Command, errores y respuesta**

```csharp
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CreateCustomerCreditNote;

public sealed record CreateCustomerCreditNoteCommand(
    Guid CustomerId,
    decimal Amount,
    string Reason,
    DateTime Date,
    Guid? SaleId = null
) : IRequest<Result<CreateCustomerCreditNoteResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesCreditNoteCreate];
}
```

```csharp
using eiti.Application.Features.Customers.Common;

namespace eiti.Application.Features.Customers.Commands.CreateCustomerCreditNote;

public sealed record CreateCustomerCreditNoteResponse(
    Guid Id,
    string Code,
    decimal Amount,
    decimal CustomerCreditBalance,
    IReadOnlyList<CustomerPaymentImputacion> Imputaciones,
    decimal Sobrante);
```

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.CreateCustomerCreditNote;

public static class CreateCustomerCreditNoteErrors
{
    public static readonly Error CustomerNotFound = Error.NotFound(
        "Customers.CreateCreditNote.CustomerNotFound",
        "El cliente no existe.");

    public static readonly Error SaleNotFound = Error.NotFound(
        "Customers.CreateCreditNote.SaleNotFound",
        "La venta asociada no existe o no es de este cliente.");

    public static readonly Error SaleCancelled = Error.Conflict(
        "Customers.CreateCreditNote.SaleCancelled",
        "No se puede emitir una nota de crédito sobre una venta anulada.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Customers.CreateCreditNote.NoCashSessionOpen",
        "No hay una sesión de caja abierta para registrar la nota de crédito.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Customers.CreateCreditNote.NoAssignedCashDrawer",
        "No tenés una caja asignada para registrar la nota de crédito.");

    public static readonly Error CashSessionFromPreviousDay = Error.Conflict(
        "Customers.CreateCreditNote.CashSessionFromPreviousDay",
        "La sesión de caja abierta es de un día anterior. Cerrala antes de continuar.");
}
```

- [ ] **Step 3: Validator**

```csharp
using FluentValidation;

namespace eiti.Application.Features.Customers.Commands.CreateCustomerCreditNote;

public sealed class CreateCustomerCreditNoteValidator : AbstractValidator<CreateCustomerCreditNoteCommand>
{
    public CreateCustomerCreditNoteValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El importe de la nota de crédito debe ser mayor a cero.");

        // El motivo es la única trazabilidad de un ajuste sin documento de origen.
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo de la nota de crédito es obligatorio.")
            .MaximumLength(250).WithMessage("El motivo no puede superar los 250 caracteres.");

        RuleFor(x => x.Date).NotEmpty();
    }
}
```

- [ ] **Step 4: Escribir los tests que fallan**

```csharp
// eiti.Tests/CreateCustomerCreditNoteHandlerTests.cs
//
// Cubrir, con Moq sobre ICustomerRepository / ISaleRepository / ICustomerCreditNoteRepository /
// ICashSessionRepository / ICashDrawerRepository / ICurrentUserService / IUnitOfWork:
//
// 1. Emision_SumaAlSaldoAFavor_YSeImputaFifoALaVentaMasVieja
//    Cliente con 2 ventas CC pendientes (40k la vieja, 80k la nueva) y NC de 50k.
//    → la vieja queda cubierta (40k), la nueva recibe 10k, CreditBalance final 0.
//    → Imputaciones tiene 2 filas en orden FIFO.
//
// 2. Emision_SinVentasPendientes_TodoQuedaComoSaldoAFavor
//    → CreditBalance = importe de la NC, Imputaciones vacío, Sobrante = importe.
//
// 3. Emision_RegistraUnMovimientoNeutroEnCaja
//    → session.Movements contiene un CashMovementType.CustomerCreditNote con Direction.None
//    → session.ExpectedClosingAmount NO cambia respecto de antes de emitir.
//
// 4. Emision_ConSaleIdDeOtroCliente_DevuelveSaleNotFound
//
// 5. Emision_SobreVentaAnulada_DevuelveSaleCancelled
//
// 6. Emision_SinSesionDeCajaAbierta_DevuelveNoCashSessionOpen
//
// 7. Emision_LasImputacionesLlevanElCreditNoteId
//    → toda fila SaleCcPayment creada tiene CreditNoteId == note.Id y NUNCA Guid.Empty.
//    Este es el test que protege el diseño: si alguien vuelve a pasar null al applicator, cae.
```

**Molde obligatorio:** `eiti.Tests/AddCustomerPaymentHandlerTests.cs`. Ese archivo ya arma los
mocks de `ICurrentUserService`, `ICashDrawerRepository` y `ICashSessionRepository` que
`CashSessionResolver` necesita — que es la parte difícil de estos tests. Leerlo entero y copiar
su `BuildHandler` / setup, cambiando el handler bajo prueba.

Los 7 casos de arriba son la especificación de qué cubrir; escribirlos con código real siguiendo
ese molde, no dejarlos como comentarios.

- [ ] **Step 5: Correr y verificar que fallan**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~CreateCustomerCreditNoteHandlerTests"`
Expected: FAIL — el handler no existe.

- [ ] **Step 6: Handler**

Estructura (leer `AddCustomerPaymentHandler.cs` L58-200 como molde exacto de resolución de caja y confirmación de stock):

```csharp
public async Task<Result<CreateCustomerCreditNoteResponse>> Handle(
    CreateCustomerCreditNoteCommand command, CancellationToken cancellationToken)
{
    var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
    if (authCheck.IsFailure)
        return Result<CreateCustomerCreditNoteResponse>.Failure(authCheck.Error);

    var companyId = _currentUserService.CompanyId!;
    var userId = _currentUserService.UserId!;

    var customer = await _customerRepository.GetByIdAsync(
        new CustomerId(command.CustomerId), companyId, cancellationToken);
    if (customer is null)
        return Result<CreateCustomerCreditNoteResponse>.Failure(
            CreateCustomerCreditNoteErrors.CustomerNotFound);

    // Venta asociada opcional: si viene, tiene que ser de este cliente y no estar anulada.
    if (command.SaleId.HasValue)
    {
        var sale = await _saleRepository.GetByIdAsync(
            new SaleId(command.SaleId.Value), companyId, cancellationToken);

        if (sale is null || sale.CustomerId?.Value != command.CustomerId)
            return Result<CreateCustomerCreditNoteResponse>.Failure(
                CreateCustomerCreditNoteErrors.SaleNotFound);

        if (sale.SaleStatus == SaleStatus.Cancel)
            return Result<CreateCustomerCreditNoteResponse>.Failure(
                CreateCustomerCreditNoteErrors.SaleCancelled);
    }

    // Misma exigencia que un cobro: la NC es un hecho del turno y se ve en la sesión.
    var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
        _currentUserService, _cashDrawerRepository, _cashSessionRepository,
        userId, companyId, cancellationToken);
    if (resolve.Status != CashSessionResolveStatus.Resolved)
        return Result<CreateCustomerCreditNoteResponse>.Failure(
            resolve.Status == CashSessionResolveStatus.NoAssignedDrawer
                ? CreateCustomerCreditNoteErrors.NoAssignedCashDrawer
                : CreateCustomerCreditNoteErrors.NoCashSessionOpen);
    var session = resolve.Session!;

    if (BusinessDay.IsFromPreviousBusinessDay(session.OpenedAt))
        return Result<CreateCustomerCreditNoteResponse>.Failure(
            CreateCustomerCreditNoteErrors.CashSessionFromPreviousDay);

    // Numeración NCC-### por sucursal. Hereda el esquema por conteo de ventas y compras:
    // no es AFIP-válido y se resuelve en el proyecto fiscal, en los tres lugares a la vez.
    var count = await _creditNoteRepository.CountByBranchAsync(
        companyId.Value, session.BranchId.Value, cancellationToken);
    var code = $"NCC-{(count + 1).ToString().PadLeft(3, '0')}";

    var note = CustomerCreditNote.Create(
        companyId.Value,
        customer.Id.Value,
        session.BranchId.Value,
        code,
        command.Amount,
        command.Reason,
        command.Date,
        command.SaleId,
        userId.Value);

    await _creditNoteRepository.AddAsync(note, cancellationToken);

    customer.AddCredit(note.Amount);

    // Imputación FIFO con back-link a la NC: sin creditNoteId, anularla no podría deshacerla.
    var application = await CustomerCreditApplicator.ApplyToPendingCcSalesAsync(
        customer, companyId, _saleRepository, cancellationToken,
        customerPaymentId: null, creditNoteId: note.Id);
    _customerRepository.Update(customer);

    // Confirmar stock de las ventas que pasaron a Paid. Reusa las entidades tracked del
    // applicator — copiar el bloque de ApplyCustomerCreditHandler.cs L63-89 tal cual.
    foreach (var sale in application.SalesNowPaidEntities)
    {
        // ... idéntico a ApplyCustomerCreditHandler, con la descripción:
        // "Stock confirmed as sold (CC paid via credit note)."
    }

    // Movimiento neutro: visible en la sesión, ExpectedClosingAmount no se mueve.
    session.RegisterCustomerCreditNote(note.Amount, note.Id, note.Code, userId);

    await _unitOfWork.SaveChangesAsync(cancellationToken);

    var imputado = application.Imputaciones.Sum(i => i.Amount);

    return Result<CreateCustomerCreditNoteResponse>.Success(new CreateCustomerCreditNoteResponse(
        note.Id,
        note.Code,
        note.Amount,
        customer.CreditBalance,
        application.Imputaciones,
        decimal.Round(note.Amount - imputado, 2, MidpointRounding.AwayFromZero)));
}
```

- [ ] **Step 7: Endpoint**

En `CustomersController.cs`, junto a `ApplyCustomerCredit` (L141):

```csharp
    [HttpPost("{id:guid}/credit-notes")]
    public async Task<IActionResult> CreateCustomerCreditNote(
        Guid id,
        [FromBody] CreateCustomerCreditNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateCustomerCreditNoteCommand(id, request.Amount, request.Reason, request.Date, request.SaleId),
            cancellationToken);

        return result.ToActionResult();
    }
```

Con el record de request al lado de los demás del controller:

```csharp
public sealed record CreateCustomerCreditNoteRequest(
    decimal Amount,
    string Reason,
    DateTime Date,
    Guid? SaleId);
```

> Si el controller usa otro helper que `ToActionResult()`, copiar el que usan sus vecinos.

- [ ] **Step 8: Correr y verificar que pasan**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~CreateCustomerCreditNoteHandlerTests"`
Expected: PASS (7 tests)

- [ ] **Step 9: Commit**

```bash
git add eiti.Application/Features/Customers/ eiti.Api/Controllers/CustomersController.cs eiti.Tests/CreateCustomerCreditNoteHandlerTests.cs
git commit -m "feat(clientes): emitir nota de credito con imputacion FIFO"
```

---

## Task 8: Anular nota de crédito de cliente

**Files:**
- Create: `eiti.Application/Features/Customers/Commands/CancelCustomerCreditNote/` (Command, Handler, Errors, Response)
- Modify: `eiti.Api/Controllers/CustomersController.cs`
- Test: `eiti.Tests/CancelCustomerCreditNoteHandlerTests.cs`

**Interfaces:**
- Consumes: `ISaleRepository.ListByCreditNoteIdAsync`, `Sale.RevertCreditNote`, `CashSession.RegisterCustomerCreditNoteCancellation`, `PermissionCodes.SalesCreditNoteCancel`
- Produces: `CancelCustomerCreditNoteResponse(Guid Id, string Code, decimal CustomerCreditBalance)`

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
// eiti.Tests/CancelCustomerCreditNoteHandlerTests.cs
//
// 1. Anulacion_RevierteSoloSusPropiasImputaciones
//    Venta con imputaciones de la NC A, de la NC B y de un cobro.
//    → tras anular A, siguen activas las de B y las del cobro.
//    Es el test central: si alguien rompe el back-link, cae acá.
//
// 2. Anulacion_DevuelveLaVentaAPendiente_YElStockAReservado
//    → sale.SaleStatus vuelve a OnHold y se registra un StockMovement de tipo Reserve.
//
// 3. Anulacion_RevierteElCreditoNoConsumido
//    NC de 50k sin ventas pendientes → CreditBalance baja 50k.
//
// 4. Anulacion_SinSaldoSuficiente_DevuelveCreditAlreadyConsumed
//    NC de 50k, crédito ya gastado en otra venta que después se anuló.
//    → falla con CreditAlreadyConsumed y NO deja CreditBalance negativo.
//
// 5. Anulacion_RegistraMovimientoNeutroDeCancelacion
//    → CashMovementType.CustomerCreditNoteCancellation con Direction.None
//    → ExpectedClosingAmount sin cambios.
//
// 6. Anulacion_DeUnaNotaYaAnulada_DevuelveAlreadyCancelled
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~CancelCustomerCreditNoteHandlerTests"`
Expected: FAIL — el handler no existe.

- [ ] **Step 3: Errores**

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerCreditNote;

public static class CancelCustomerCreditNoteErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Customers.CancelCreditNote.NotFound",
        "La nota de crédito no existe.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Customers.CancelCreditNote.AlreadyCancelled",
        "La nota de crédito ya está anulada.");

    // El crédito de la NC ya se gastó y el saldo actual no alcanza para revertirlo:
    // anularla dejaría el saldo a favor en negativo.
    public static readonly Error CreditAlreadyConsumed = Error.Conflict(
        "Customers.CancelCreditNote.CreditAlreadyConsumed",
        "El saldo a favor generado por esta nota de crédito ya fue utilizado y no se puede revertir.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Customers.CancelCreditNote.NoCashSessionOpen",
        "No hay una sesión de caja abierta para registrar la anulación.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Customers.CancelCreditNote.NoAssignedCashDrawer",
        "No tenés una caja asignada para registrar la anulación.");
}
```

- [ ] **Step 4: Command y respuesta**

```csharp
public sealed record CancelCustomerCreditNoteCommand(Guid CustomerId, Guid CreditNoteId)
    : IRequest<Result<CancelCustomerCreditNoteResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesCreditNoteCancel];
}

public sealed record CancelCustomerCreditNoteResponse(Guid Id, string Code, decimal CustomerCreditBalance);
```

- [ ] **Step 5: Handler**

```csharp
// ... auth, companyId, userId como en Task 7 ...

var note = await _creditNoteRepository.GetByIdAsync(command.CreditNoteId, companyId.Value, cancellationToken);
if (note is null || note.CustomerId != command.CustomerId)
    return Result<CancelCustomerCreditNoteResponse>.Failure(CancelCustomerCreditNoteErrors.NotFound);

if (note.Status == CreditNoteStatus.Cancelled)
    return Result<CancelCustomerCreditNoteResponse>.Failure(CancelCustomerCreditNoteErrors.AlreadyCancelled);

var customer = await _customerRepository.GetByIdAsync(new CustomerId(note.CustomerId), companyId, cancellationToken);
if (customer is null)
    return Result<CancelCustomerCreditNoteResponse>.Failure(CancelCustomerCreditNoteErrors.NotFound);

var resolve = await CashSessionResolver.ResolveOpenSessionAsync(/* igual que Task 7 */);
// ... mismos dos errores de caja ...
var session = resolve.Session!;

// Deshacer las imputaciones de ESTA NC. Las de otros orígenes no se tocan.
var sales = await _saleRepository.ListByCreditNoteIdAsync(companyId, note.Id, cancellationToken);
var imputedTotal = 0m;
foreach (var sale in sales)
{
    imputedTotal += sale.CcPayments
        .Where(p => p.CreditNoteId == note.Id && p.Status == SaleCcPaymentStatus.Active)
        .Sum(p => p.Amount);

    var revertedFromPaid = sale.RevertCreditNote(note.Id);

    if (revertedFromPaid)
    {
        // Stock de vuelta a reservado. Copiar el bloque de CustomerPaymentReversal.cs L42-66,
        // con la descripción "Stock reverted to reserved (credit note cancelled)."
    }
}

// El crédito que la NC generó y todavía no se gastó tiene que volver atrás.
var creditToRevert = Math.Max(0m, note.Amount - imputedTotal);
if (creditToRevert > customer.CreditBalance)
    return Result<CancelCustomerCreditNoteResponse>.Failure(
        CancelCustomerCreditNoteErrors.CreditAlreadyConsumed);

if (creditToRevert > 0m)
    customer.ConsumeCredit(creditToRevert);

_customerRepository.Update(customer);

session.RegisterCustomerCreditNoteCancellation(note.Amount, note.Id, note.Code, userId);
note.Cancel(userId.Value);

await _unitOfWork.SaveChangesAsync(cancellationToken);

return Result<CancelCustomerCreditNoteResponse>.Success(
    new CancelCustomerCreditNoteResponse(note.Id, note.Code, customer.CreditBalance));
```

> La validación de `creditToRevert > customer.CreditBalance` va **antes** de cualquier mutación del saldo: es lo que evita dejar `CreditBalance` negativo.

- [ ] **Step 6: Endpoint**

```csharp
    [HttpDelete("{id:guid}/credit-notes/{creditNoteId:guid}")]
    public async Task<IActionResult> CancelCustomerCreditNote(
        Guid id,
        Guid creditNoteId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CancelCustomerCreditNoteCommand(id, creditNoteId), cancellationToken);

        return result.ToActionResult();
    }
```

- [ ] **Step 7: Correr y verificar que pasan**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~CancelCustomerCreditNoteHandlerTests"`
Expected: PASS (6 tests)

- [ ] **Step 8: Commit**

```bash
git add eiti.Application/Features/Customers/Commands/CancelCustomerCreditNote/ eiti.Api/Controllers/CustomersController.cs eiti.Tests/CancelCustomerCreditNoteHandlerTests.cs
git commit -m "feat(clientes): anular nota de credito revirtiendo sus imputaciones"
```

---

## Task 9: Notas de crédito de proveedor (emitir y anular)

**Files:**
- Create: `eiti.Application/Features/Suppliers/Commands/CreateSupplierCreditNote/` (slice completo)
- Create: `eiti.Application/Features/Suppliers/Commands/CancelSupplierCreditNote/` (slice completo)
- Modify: `eiti.Application/Features/Purchases/Common/SupplierCreditApplicator.cs`
- Modify: `eiti.Api/Controllers/SuppliersController.cs`
- Test: `eiti.Tests/SupplierCreditNoteHandlersTests.cs`

**Interfaces:**
- Consumes: `SupplierCreditNote`, `Purchase.RevertCreditNote`, `IPurchaseRepository.ListByCreditNoteIdAsync`, `CashSession.RegisterSupplierCreditNote*`, `PermissionCodes.PurchasesCreditNote*`
- Produces: `CreateSupplierCreditNoteResponse(Guid Id, string Code, decimal Amount, decimal SupplierCreditBalance, IReadOnlyList<SupplierPaymentImputacion> Imputaciones, decimal Sobrante)`; `CancelSupplierCreditNoteResponse(Guid Id, string Code, decimal SupplierCreditBalance)`

- [ ] **Step 1: Extender el applicator de proveedores**

En `SupplierCreditApplicator.ApplyToPendingPurchasesAsync`, sumar `Guid? creditNoteId = null` al final de la firma y pasarlo en el `PurchasePayment.Create(...)`:

```csharp
            purchase.AddPayment(PurchasePayment.Create(
                PurchasePaymentMethod.SupplierCredit,
                applied,
                DateTime.UtcNow,
                null,
                "Saldo a favor aplicado automáticamente",
                supplierPaymentId: supplierPaymentId,
                creditNoteId: creditNoteId));
```

- [ ] **Step 2: Escribir los tests que fallan**

```csharp
// eiti.Tests/SupplierCreditNoteHandlersTests.cs
//
// Espejo de los tests de cliente, sobre compras:
// 1. Emision_SumaAlSaldoAFavor_YSeImputaFifoALaCompraMasVieja
// 2. Emision_SinComprasPendientes_TodoQuedaComoSaldoAFavor
// 3. Emision_RegistraMovimientoNeutro_YNoAlteraElArqueo
// 4. Emision_LasImputacionesLlevanElCreditNoteId
// 5. Anulacion_RevierteSoloSusPropiasImputaciones
// 6. Anulacion_SinSaldoSuficiente_DevuelveCreditAlreadyConsumed
// 7. Anulacion_DeUnaNotaYaAnulada_DevuelveAlreadyCancelled
```

- [ ] **Step 3: Correr y verificar que fallan**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~SupplierCreditNoteHandlersTests"`
Expected: FAIL — los handlers no existen.

- [ ] **Step 4: Slice de emisión**

Espejo exacto de Task 7, con estos cambios:
- Namespace `eiti.Application.Features.Suppliers.Commands.CreateSupplierCreditNote`
- `SupplierId` / `PurchaseId` en lugar de `CustomerId` / `SaleId`
- Prefijo de código `NCP-`
- Permiso `PermissionCodes.PurchasesCreditNoteCreate`
- Códigos de error con prefijo `Suppliers.CreateCreditNote.*`
- `SupplierCreditApplicator.ApplyToPendingPurchasesAsync(supplier, companyId.Value, _purchaseRepository, excludePurchaseId: null, cancellationToken, supplierPaymentId: null, creditNoteId: note.Id)`
- `session.RegisterSupplierCreditNote(note.Amount, note.Id, note.Code, userId)`
- **Sin bloque de confirmación de stock**: las compras no reservan stock como las ventas CC.

- [ ] **Step 5: Slice de anulación**

Espejo exacto de Task 8, con:
- `_purchaseRepository.ListByCreditNoteIdAsync(companyId.Value, note.Id, cancellationToken)`
- `imputedTotal += purchase.RevertCreditNote(note.Id)` — el método del agregado ya devuelve el total desimputado, así que no hace falta sumar las filas a mano
- `session.RegisterSupplierCreditNoteCancellation(...)`
- Misma guarda de `creditToRevert > supplier.CreditBalance` → `CreditAlreadyConsumed`, **antes** de mutar
- Sin bloque de stock

- [ ] **Step 6: Endpoints**

En `SuppliersController.cs`, junto a los de payments:

```csharp
    [HttpPost("{id:guid}/credit-notes")]
    public async Task<IActionResult> CreateSupplierCreditNote(
        Guid id,
        [FromBody] CreateSupplierCreditNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateSupplierCreditNoteCommand(id, request.Amount, request.Reason, request.Date, request.PurchaseId),
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}/credit-notes/{creditNoteId:guid}")]
    public async Task<IActionResult> CancelSupplierCreditNote(
        Guid id,
        Guid creditNoteId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CancelSupplierCreditNoteCommand(id, creditNoteId), cancellationToken);

        return result.ToActionResult();
    }
```

- [ ] **Step 7: Correr y verificar que pasan**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj`
Expected: suite completa en verde.

- [ ] **Step 8: Commit**

```bash
git add eiti.Application/Features/Suppliers/ eiti.Application/Features/Purchases/Common/SupplierCreditApplicator.cs eiti.Api/Controllers/SuppliersController.cs eiti.Tests/SupplierCreditNoteHandlersTests.cs
git commit -m "feat(proveedores): emitir y anular notas de credito"
```

---

## Task 10: Estados de cuenta

**Files:**
- Modify: `eiti.Application/Features/Customers/Queries/GetCustomerAccount/GetCustomerAccountQuery.cs`
- Modify: `eiti.Application/Features/Customers/Queries/GetCustomerAccount/GetCustomerAccountHandler.cs`
- Modify: `eiti.Application/Features/Suppliers/Queries/GetSupplierAccount/GetSupplierAccountQuery.cs`
- Modify: `eiti.Application/Features/Suppliers/Queries/GetSupplierAccount/GetSupplierAccountHandler.cs`
- Test: `eiti.Tests/CreditNoteAccountStatementTests.cs`

**Interfaces:**
- Consumes: `ICustomerCreditNoteRepository.ListByCustomerAsync`, `ISupplierCreditNoteRepository.ListBySupplierAsync`
- Produces: `CustomerAccountMovement.Type` acepta `"nota_credito"`; ídem `SupplierAccountMovement`

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
// eiti.Tests/CreditNoteAccountStatementTests.cs
//
// 1. EstadoDeCuenta_MuestraLaNotaDeCreditoComoMovimientoPropio
//    → existe un movement con Type == "nota_credito", IsDebit == false,
//      Description contiene el Reason y Code es el NCC-###.
//
// 2. EstadoDeCuenta_LaNotaDeCredito_BajaSaldoPendiente_YNoTocaCobradoTotal
//    Cliente con 1 venta de 100k y NC de 30k.
//    → CobradoTotal == 0 (no entró dinero)
//    → SaldoPendiente == 70k
//    → DeudaTotal == 100k (el bruto facturado no se toca)
//    Es el test que importa: si la NC fuera a cobranzas, el estado de cuenta diría
//    que se cobró plata que nadie pagó.
//
// 3. EstadoDeCuenta_IgnoraLasNotasDeCreditoAnuladas
//
// 4. EstadoDeCuenta_Proveedor_MismoTratamiento
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~CreditNoteAccountStatementTests"`
Expected: FAIL — no hay movimientos `"nota_credito"`.

- [ ] **Step 3: Actualizar el contrato**

En `GetCustomerAccountQuery.cs`, el comentario del campo `Type`:

```csharp
    string Type,            // "venta" | "cobro" | "nota_credito"
```

Ídem en `GetSupplierAccountQuery.cs`: `// "compra" | "pago" | "nota_credito"`.

No hacen falta campos nuevos: `Code`, `Amount`, `Description`, `Imputaciones` y `Sobrante` ya cubren la NC.

- [ ] **Step 4: Handler de cliente**

Inyectar `ICustomerCreditNoteRepository` y sumar los movimientos:

```csharp
        var creditNotes = await _creditNoteRepository.ListByCustomerAsync(
            companyId.Value, customer.Id.Value, cancellationToken);

        foreach (var note in creditNotes.Where(n => n.Status == CreditNoteStatus.Active))
        {
            movements.Add(new CustomerAccountMovement(
                Type: "nota_credito",
                Id: note.Id,
                Date: note.Date,
                Description: note.Reason,
                Code: note.Code,
                Amount: note.Amount,
                IsDebit: false,
                Status: (int)note.Status,
                StatusName: "Activa",
                Method: null,
                ChequeNumero: null,
                Imputaciones: imputacionesPorNota.TryGetValue(note.Id, out var imp) ? imp : [],
                Sobrante: decimal.Round(
                    note.Amount - (imputacionesPorNota.TryGetValue(note.Id, out var i) ? i.Sum(x => x.Amount) : 0m),
                    2, MidpointRounding.AwayFromZero),
                Reference: null,
                Notes: note.Reason,
                SortDate: note.Date));
        }
```

`imputacionesPorNota` se arma recorriendo las ventas que el handler ya tiene cargadas, igual que
se arman las de un cobro:

```csharp
        var imputacionesPorNota = sales
            .SelectMany(s => s.CcPayments
                .Where(p => p.CreditNoteId.HasValue && p.Status == SaleCcPaymentStatus.Active)
                .Select(p => new { NoteId = p.CreditNoteId!.Value, Sale = s, p.Amount }))
            .GroupBy(x => x.NoteId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CustomerPaymentImputacion>)g
                    .Select(x => new CustomerPaymentImputacion(
                        x.Sale.Id.Value, x.Sale.Code ?? string.Empty, x.Amount))
                    .ToList());
```

Y en el cálculo de totales: **la NC resta de `SaldoPendiente` y no suma a `CobradoTotal`**. Buscar dónde se computan `CobradoTotal` y `SaldoPendiente` en el handler y restar el total de NC activas solo del segundo.

- [ ] **Step 5: Handler de proveedor**

Mismo cambio con `ISupplierCreditNoteRepository`, `SupplierAccountMovement`, y restando de `SaldoPendiente` sin tocar `PagadoTotal`.

- [ ] **Step 6: Correr y verificar que pasan**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj`
Expected: suite completa en verde.

- [ ] **Step 7: Commit**

```bash
git add eiti.Application/Features/Customers/Queries/ eiti.Application/Features/Suppliers/Queries/ eiti.Tests/CreditNoteAccountStatementTests.cs
git commit -m "feat(cuentas): la nota de credito baja el saldo pendiente sin tocar lo cobrado"
```

---

## Task 11: Frontend — modelos, servicios y etiquetas de caja

**Files:**
- Create: `C:/EiTeFront/eiti-front/src/app/core/models/credit-note.models.ts`
- Modify: `src/app/core/services/customer-account.service.ts`
- Modify: `src/app/core/services/supplier-account.service.ts`
- Modify: `src/app/core/models/customer-account.models.ts` · `supplier-account.models.ts`
- Modify: `src/app/features/cash/cash.component.ts`

**Interfaces:**
- Produces: `CreateCreditNoteRequest`, `CreateCreditNoteResult`, `CreditNoteMovement`; `CustomerAccountService.createCreditNote(customerId, req)` / `.cancelCreditNote(customerId, creditNoteId)`; ídem en `SupplierAccountService`

- [ ] **Step 1: Modelos**

```typescript
// src/app/core/models/credit-note.models.ts

/** Nota de crédito: ajusta el saldo sin mover mercadería ni dinero. */
export interface CreateCreditNoteRequest {
  amount: number;
  reason: string;
  date: string;
  /** Venta (cliente) o compra (proveedor) asociada. Opcional. */
  saleId?: string | null;
  purchaseId?: string | null;
}

export interface CreditNoteImputacion {
  code: string;
  amount: number;
}

export interface CreateCreditNoteResult {
  id: string;
  code: string;
  amount: number;
  imputaciones: CreditNoteImputacion[];
  sobrante: number;
}
```

En `customer-account.models.ts`, el tipo del movimiento debe aceptar `'nota_credito'`. Buscar la union de `type` y sumarlo. Ídem en `supplier-account.models.ts`.

- [ ] **Step 2: Servicios**

En `customer-account.service.ts`:

```typescript
  createCreditNote(customerId: string, req: CreateCreditNoteRequest): Observable<CreateCreditNoteResult> {
    return this.http.post<CreateCreditNoteResult>(
      `${this.base}/customers/${customerId}/credit-notes`, req);
  }

  cancelCreditNote(customerId: string, creditNoteId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.base}/customers/${customerId}/credit-notes/${creditNoteId}`);
  }
```

En `supplier-account.service.ts` (ojo: su `base` ya incluye `/suppliers`):

```typescript
  createCreditNote(supplierId: string, req: CreateCreditNoteRequest): Observable<CreateCreditNoteResult> {
    return this.http.post<CreateCreditNoteResult>(`${this.base}/${supplierId}/credit-notes`, req);
  }

  cancelCreditNote(supplierId: string, creditNoteId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${supplierId}/credit-notes/${creditNoteId}`);
  }
```

- [ ] **Step 3: Etiquetas de caja**

En `cash.component.ts`, en el mapa de `translateType` (~L2336, junto a `CuentaCorrienteIncome` y `PurchaseExpense`):

```typescript
            CustomerCreditNote: 'NC a cliente',
            CustomerCreditNoteCancellation: 'NC a cliente anulada',
            SupplierCreditNote: 'NC de proveedor',
            SupplierCreditNoteCancellation: 'NC de proveedor anulada',
```

Y los badges en el CSS del componente (~L962), siguiendo el molde de los existentes:

```css
    .badge--type[data-type="CustomerCreditNote"]{background:color-mix(in srgb,#f59e0b 12%, transparent);color:#f59e0b;border:1px solid color-mix(in srgb,#f59e0b 30%, transparent)}
    .badge--type[data-type="CustomerCreditNoteCancellation"]{background:color-mix(in srgb,#f59e0b 12%, transparent);color:#f59e0b;border:1px solid color-mix(in srgb,#f59e0b 30%, transparent)}
    .badge--type[data-type="SupplierCreditNote"]{background:color-mix(in srgb,#8b5cf6 12%, transparent);color:#8b5cf6;border:1px solid color-mix(in srgb,#8b5cf6 30%, transparent)}
    .badge--type[data-type="SupplierCreditNoteCancellation"]{background:color-mix(in srgb,#8b5cf6 12%, transparent);color:#8b5cf6;border:1px solid color-mix(in srgb,#8b5cf6 30%, transparent)}
```

- [ ] **Step 4: Build**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: 0 errores.

- [ ] **Step 5: Commit**

```bash
cd C:/EiTeFront/eiti-front
git add src/app/core/ src/app/features/cash/
git commit -m "feat(front): modelos, servicios y etiquetas de caja para notas de credito"
```

---

## Task 12: Frontend — UI de cuenta corriente de cliente

**Files:**
- Modify: `src/app/features/clients/customer-account.component.ts` · `.html` · `.css`
- Test: `src/app/features/clients/customer-account.component.spec.ts`

**Interfaces:**
- Consumes: `CustomerAccountService.createCreditNote` / `.cancelCreditNote`, `CreateCreditNoteRequest`

> **Antes de escribir markup, invocar la skill `/frontend-design`** (regla del CLAUDE.md del proyecto). El modal debe seguir el lenguaje visual del modal de cobro que ya existe en esta pantalla.

- [ ] **Step 1: Escribir los tests que fallan**

```typescript
// customer-account.component.spec.ts — sumar:
//
// 1. 'no permite emitir una nota de credito sin motivo'
//    → con importe cargado y motivo vacío, el submit no llama al servicio.
//
// 2. 'emite la nota de credito con los datos del formulario'
//    → createCreditNote llamado con { amount, reason, date, saleId }.
//
// 3. 'anular pide confirmacion antes de llamar al servicio'
//    → con ConfirmationService devolviendo false, cancelCreditNote NO se llama.
//
// 4. 'muestra la nota de credito como movimiento propio'
//    → una fila con type 'nota_credito' se renderiza con su código y motivo.
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `cd C:/EiTeFront/eiti-front && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: FAIL en los 4 nuevos.

- [ ] **Step 3: Implementar**

- Botón "Nota de crédito" junto al de registrar cobro, gateado por `auth.hasPermission(PermissionCodes.salesCreditNoteCreate)`.
- Modal con importe, motivo (requerido), fecha y venta asociada opcional. Reactive Forms, `Validators.required` + `Validators.maxLength(250)` en el motivo.
- Para elegir la venta: `app-searchable-select`. **Nunca un `<select>` nativo** (`.claude/rules/lessons.md`, 2026-07-22).
- La fecha por defecto se calcula con `toLocaleDateString('en-CA')`, **nunca** `toISOString()` (`.claude/rules/lessons.md`, 2026-08-15).
- Fila del estado de cuenta para `type === 'nota_credito'`, con su código, motivo e importe en la columna de haber.
- Botón de anular en esa fila, gateado por `salesCreditNoteCancel`, que pasa por `ConfirmationService` mostrando el importe y advirtiendo que si el crédito ya se imputó, esas ventas vuelven a quedar impagas.
- Errores del backend por `ToastService` leyendo `error.error.detail`.

- [ ] **Step 4: Correr los tests y el build**

```bash
cd C:/EiTeFront/eiti-front
npx ng test --watch=false --browsers=ChromeHeadless
ng build --configuration development
```
Expected: suite en verde, build sin errores.

- [ ] **Step 5: Commit**

```bash
git add src/app/features/clients/
git commit -m "feat(front): emitir y anular notas de credito en cuenta corriente"
```

---

## Task 13: Frontend — UI de cuenta de proveedor

**Files:**
- Modify: el componente de cuenta de proveedor (resolver con `grep -rn "supplier-account.service" src/app/features --include=*.ts`)
- Test: su `.spec.ts`

**Interfaces:**
- Consumes: `SupplierAccountService.createCreditNote` / `.cancelCreditNote`

- [ ] **Step 1: Escribir los tests que fallan**

```typescript
// spec del componente de cuenta de proveedor — sumar:
//
// 1. 'no permite emitir una nota de credito sin motivo'
//    → con importe cargado y motivo vacío, el submit no llama al servicio.
//
// 2. 'emite la nota de credito con los datos del formulario'
//    → createCreditNote llamado con { amount, reason, date, purchaseId }.
//
// 3. 'anular pide confirmacion antes de llamar al servicio'
//    → con ConfirmationService devolviendo false, cancelCreditNote NO se llama.
//
// 4. 'muestra la nota de credito como movimiento propio'
//    → una fila con type 'nota_credito' se renderiza con su código y motivo.
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `cd C:/EiTeFront/eiti-front && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: FAIL en los 4 nuevos.

- [ ] **Step 3: Implementar**

Espejo de Task 12 con: permisos `purchasesCreditNoteCreate` / `purchasesCreditNoteCancel`, compra asociada en lugar de venta, y `"nota_credito"` en el estado de cuenta del proveedor.

- [ ] **Step 4: Correr los tests y el build**

```bash
cd C:/EiTeFront/eiti-front
npx ng test --watch=false --browsers=ChromeHeadless
ng build --configuration development
```
Expected: suite en verde, build sin errores.

- [ ] **Step 5: Verificación final de las dos suites**

```bash
cd C:/Eiti/eiti && dotnet test eiti.Tests/eiti.Tests.csproj
cd C:/EiTeFront/eiti-front && npx ng test --watch=false --browsers=ChromeHeadless
```
Expected: ambas en verde.

- [ ] **Step 6: Commit**

```bash
git add src/app/features/
git commit -m "feat(front): notas de credito en la cuenta de proveedor"
```

---

## Verificación manual antes de integrar

Con la API corriendo y una caja abierta:

1. Emitir una NC de 30.000 a un cliente con una venta CC de 100.000 pendiente.
   → la venta queda con 70.000 pendientes; el estado de cuenta muestra la NC como fila propia.
2. Abrir la caja del día.
   → la NC figura en los movimientos, con importe y **sin flecha de entrada ni de salida**.
   → **el efectivo esperado no cambió.** Ésta es la verificación que no puede fallar.
3. Anular la NC.
   → la venta vuelve a 100.000 pendientes; aparece el movimiento de anulación, también neutro.
4. Emitir una NC de 200.000 a un cliente sin deuda.
   → queda íntegra como saldo a favor; la próxima venta CC la consume sola.
5. Repetir 1-3 del lado proveedor.
