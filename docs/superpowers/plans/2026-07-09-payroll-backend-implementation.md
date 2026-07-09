# Payroll (pago de salarios) — Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the backend (domain, infrastructure, application, API) for the payroll module described in `docs/superpowers/specs/2026-07-09-payroll-design.md`, so liquidations can be generated, paid, and cancelled via API, with cash-drawer integration for cash payments.

**Architecture:** Vertical-slice CQRS with MediatR, `Result<T>` (no exceptions for business errors), independent aggregates (`PayrollDeductionConcept`, `PayrollAdvance`, `PayrollLiquidation`) — no "period" container aggregate. Mirrors the existing `Purchases`/`Suppliers` and `CashSessions` slices exactly.

**Tech Stack:** .NET 10, EF Core (Npgsql), MediatR, FluentValidation, xUnit + FluentAssertions + Moq (existing test stack, confirmed via `eiti.Tests/SaleSettlementTests.cs` and `CreateSaleHandlerTests.cs`).

**Scope note:** This plan covers backend only. Frontend (Angular) is a separate follow-up plan, written after these endpoints exist and their response shapes are confirmed — the frontend can't be meaningfully built or tested against contracts that don't exist yet.

## Global Constraints

- Every handler starts with `_currentUserService.EnsureAuthenticated()` (or `EnsureAuthenticatedWithContext()` when `UserId` is needed), per `CLAUDE.md` Handler Rules.
- No inline error strings in handlers — every error is a `static readonly Error` in a `<Feature>Errors.cs` file.
- Enum validation uses `Enum.IsDefined(typeof(T), value)`, never `InclusiveBetween`.
- Money fields use `decimal(18,2)` in EF configs; percentages use `decimal(5,2)`.
- All child collections use `private readonly List<T> _x = []` + `IReadOnlyCollection<T> X => _x` + `builder.Navigation(...).UsePropertyAccessMode(PropertyAccessMode.Field)`.
- New permissions must be added in all three places: `PermissionCodes.cs`, `RoleCatalog.cs` (Owner + Admin), `PermissionCatalog.cs` (`All`) — per `.claude/rules/lessons.md`, missing the third one breaks access-profile assignment silently.
- Build with dependencies: `dotnet build eiti.Application/eiti.Application.csproj` (never `--no-dependencies` for a final check).
- Migrations: `dotnet ef migrations add <Name> --project eiti.Infrastructure --startup-project eiti.Api --output-dir Migrations` (standard convention for this Clean-Architecture layout; the exact command was not previously documented in the repo — verify it runs cleanly in Task 6, and if the API process has locked DLLs, close it first).

---

## Task 1: `PayrollPeriodicity` enum + `Employee` payroll config

**Files:**
- Create: `eiti.Domain/Employees/PayrollPeriodicity.cs`
- Modify: `eiti.Domain/Employees/Employee.cs`
- Test: `eiti.Tests/EmployeePayrollConfigTests.cs`

**Interfaces:**
- Produces: `PayrollPeriodicity` enum (`Monthly = 1, Biweekly = 2`); `Employee.BaseSalary: decimal?`; `Employee.PayrollPeriodicity: PayrollPeriodicity?`; `Employee.SetPayrollConfig(decimal? baseSalary, PayrollPeriodicity? periodicity)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using FluentAssertions;

namespace eiti.Tests;

public sealed class EmployeePayrollConfigTests
{
    private static Employee CreateEmployee()
    {
        var companyId = CompanyId.New();
        return Employee.Create(companyId, null, "Juan", "Perez", null, null, null, EmployeeRole.Seller);
    }

    [Fact]
    public void SetPayrollConfig_ShouldSetValues_WhenValid()
    {
        var employee = CreateEmployee();

        employee.SetPayrollConfig(500000m, PayrollPeriodicity.Monthly);

        employee.BaseSalary.Should().Be(500000m);
        employee.PayrollPeriodicity.Should().Be(PayrollPeriodicity.Monthly);
    }

    [Fact]
    public void SetPayrollConfig_ShouldThrow_WhenBaseSalaryNegative()
    {
        var employee = CreateEmployee();

        var act = () => employee.SetPayrollConfig(-1m, PayrollPeriodicity.Monthly);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetPayrollConfig_ShouldThrow_WhenBaseSalarySetWithoutPeriodicity()
    {
        var employee = CreateEmployee();

        var act = () => employee.SetPayrollConfig(500000m, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetPayrollConfig_ShouldClearPeriodicity_WhenBaseSalaryCleared()
    {
        var employee = CreateEmployee();
        employee.SetPayrollConfig(500000m, PayrollPeriodicity.Biweekly);

        employee.SetPayrollConfig(null, null);

        employee.BaseSalary.Should().BeNull();
        employee.PayrollPeriodicity.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter EmployeePayrollConfigTests`
Expected: compile error (`PayrollPeriodicity` and `SetPayrollConfig` don't exist yet).

- [ ] **Step 3: Create the enum**

```csharp
namespace eiti.Domain.Employees;

public enum PayrollPeriodicity
{
    Monthly = 1,
    Biweekly = 2
}
```

- [ ] **Step 4: Extend `Employee`**

Add these properties near `EmployeeRole` (after line 16 of the current file):

```csharp
    public decimal? BaseSalary { get; private set; }
    public PayrollPeriodicity? PayrollPeriodicity { get; private set; }
```

Add this method after `Update(...)` (after the existing `Update` method body, before `Deactivate()`):

```csharp
    public void SetPayrollConfig(decimal? baseSalary, PayrollPeriodicity? periodicity)
    {
        if (baseSalary.HasValue && baseSalary.Value < 0)
        {
            throw new ArgumentException("Base salary cannot be negative.", nameof(baseSalary));
        }

        if (baseSalary.HasValue && !periodicity.HasValue)
        {
            throw new ArgumentException("Payroll periodicity is required when a base salary is set.", nameof(periodicity));
        }

        BaseSalary = baseSalary.HasValue ? decimal.Round(baseSalary.Value, 2, MidpointRounding.AwayFromZero) : null;
        PayrollPeriodicity = baseSalary.HasValue ? periodicity : null;
        UpdatedAt = DateTime.UtcNow;
    }
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter EmployeePayrollConfigTests`
Expected: 4 passed.

- [ ] **Step 6: Commit**

```bash
git add eiti.Domain/Employees/PayrollPeriodicity.cs eiti.Domain/Employees/Employee.cs eiti.Tests/EmployeePayrollConfigTests.cs
git commit -m "feat(payroll): agregar sueldo base y periodicidad a Employee"
```

---

## Task 2: `PayrollDeductionConcept` aggregate

**Files:**
- Create: `eiti.Domain/Payroll/PayrollDeductionConceptId.cs`
- Create: `eiti.Domain/Payroll/PayrollDeductionConcept.cs`
- Test: `eiti.Tests/PayrollDeductionConceptTests.cs`

**Interfaces:**
- Consumes: `CompanyId` (`eiti.Domain.Companies`).
- Produces: `PayrollDeductionConceptId`; `PayrollDeductionConcept` with `Id, CompanyId, Name, Percentage, IsActive, CreatedAt`, `Create(companyId, name, percentage)`, `Update(name, percentage)`, `Activate()`, `Deactivate()`.

- [ ] **Step 1: Write the failing tests**

```csharp
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using FluentAssertions;

namespace eiti.Tests;

public sealed class PayrollDeductionConceptTests
{
    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var concept = PayrollDeductionConcept.Create(CompanyId.New(), "Jubilacion", 11m);

        concept.Name.Should().Be("Jubilacion");
        concept.Percentage.Should().Be(11m);
        concept.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_ShouldThrow_WhenPercentageOutOfRange(decimal percentage)
    {
        var act = () => PayrollDeductionConcept.Create(CompanyId.New(), "Obra social", percentage);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNameEmpty()
    {
        var act = () => PayrollDeductionConcept.Create(CompanyId.New(), "  ", 5m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_ShouldChangeNameAndPercentage()
    {
        var concept = PayrollDeductionConcept.Create(CompanyId.New(), "ART", 3m);

        concept.Update("ART actualizado", 4.5m);

        concept.Name.Should().Be("ART actualizado");
        concept.Percentage.Should().Be(4.5m);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var concept = PayrollDeductionConcept.Create(CompanyId.New(), "ART", 3m);

        concept.Deactivate();

        concept.IsActive.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter PayrollDeductionConceptTests`
Expected: compile error (types don't exist).

- [ ] **Step 3: Implement**

```csharp
namespace eiti.Domain.Payroll;

public sealed record PayrollDeductionConceptId(Guid Value)
{
    public static PayrollDeductionConceptId New() => new(Guid.NewGuid());
}
```

```csharp
using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Payroll;

public sealed class PayrollDeductionConcept : AggregateRoot<PayrollDeductionConceptId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public decimal Percentage { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PayrollDeductionConcept()
    {
    }

    private PayrollDeductionConcept(PayrollDeductionConceptId id, CompanyId companyId, string name, decimal percentage)
        : base(id)
    {
        CompanyId = companyId;
        Name = NormalizeName(name);
        Percentage = NormalizePercentage(percentage);
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public static PayrollDeductionConcept Create(CompanyId companyId, string name, decimal percentage)
    {
        return new PayrollDeductionConcept(PayrollDeductionConceptId.New(), companyId, name, percentage);
    }

    public void Update(string name, decimal percentage)
    {
        Name = NormalizeName(name);
        Percentage = NormalizePercentage(percentage);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        var normalized = name.Trim();
        if (normalized.Length > 150)
        {
            throw new ArgumentException("Name cannot exceed 150 characters.", nameof(name));
        }

        return normalized;
    }

    private static decimal NormalizePercentage(decimal percentage)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentException("Percentage must be between 0 and 100.", nameof(percentage));
        }

        return decimal.Round(percentage, 2, MidpointRounding.AwayFromZero);
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter PayrollDeductionConceptTests`
Expected: 6 passed (2 from `[Theory]`).

- [ ] **Step 5: Commit**

```bash
git add eiti.Domain/Payroll/PayrollDeductionConceptId.cs eiti.Domain/Payroll/PayrollDeductionConcept.cs eiti.Tests/PayrollDeductionConceptTests.cs
git commit -m "feat(payroll): agregar aggregate PayrollDeductionConcept"
```

---

## Task 3: `PayrollAdvance` aggregate

**Files:**
- Create: `eiti.Domain/Payroll/PayrollAdvanceId.cs`
- Create: `eiti.Domain/Payroll/PayrollAdvanceStatus.cs`
- Create: `eiti.Domain/Payroll/PayrollAdvance.cs`
- Test: `eiti.Tests/PayrollAdvanceTests.cs`

**Interfaces:**
- Consumes: `CompanyId`, `EmployeeId` (`eiti.Domain.Employees`), `UserId` (`eiti.Domain.Users`), `PayrollLiquidationId` (defined in Task 4 — declared here as a forward reference since `PayrollAdvance.Apply` needs it; create a minimal `PayrollLiquidationId` record in this task and reuse it in Task 4 without redefining).
- Produces: `PayrollAdvanceStatus` (`Pending = 1, Applied = 2, Cancelled = 3`); `PayrollAdvance` with `Id, CompanyId, EmployeeId, Amount, Date, Notes, Status, AppliedToLiquidationId, CreatedByUserId, CreatedAt`, `Create(...)`, `Cancel()`, `Apply(PayrollLiquidationId)`, `Revert()`.

**Note:** To avoid a circular file dependency, `PayrollLiquidationId` is created here (Task 3) and Task 4 will *reuse* this same file rather than redefining it.

- [ ] **Step 1: Write the failing tests**

```csharp
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using eiti.Domain.Users;
using FluentAssertions;

namespace eiti.Tests;

public sealed class PayrollAdvanceTests
{
    private static PayrollAdvance CreateAdvance(decimal amount = 10000m)
    {
        return PayrollAdvance.Create(
            CompanyId.New(),
            EmployeeId.New(),
            amount,
            DateTime.UtcNow,
            "Adelanto por reparacion urgente",
            UserId.New());
    }

    [Fact]
    public void Create_ShouldStartAsPending()
    {
        var advance = CreateAdvance();

        advance.Status.Should().Be(PayrollAdvanceStatus.Pending);
        advance.AppliedToLiquidationId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldThrow_WhenAmountIsZeroOrLess()
    {
        var act = () => CreateAdvance(0m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_ShouldSetCancelled_WhenPending()
    {
        var advance = CreateAdvance();

        advance.Cancel();

        advance.Status.Should().Be(PayrollAdvanceStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldThrow_WhenNotPending()
    {
        var advance = CreateAdvance();
        advance.Cancel();

        var act = () => advance.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Apply_ShouldSetAppliedAndLiquidationId()
    {
        var advance = CreateAdvance();
        var liquidationId = PayrollLiquidationId.New();

        advance.Apply(liquidationId);

        advance.Status.Should().Be(PayrollAdvanceStatus.Applied);
        advance.AppliedToLiquidationId.Should().Be(liquidationId);
    }

    [Fact]
    public void Revert_ShouldSetPendingAndClearLiquidationId()
    {
        var advance = CreateAdvance();
        advance.Apply(PayrollLiquidationId.New());

        advance.Revert();

        advance.Status.Should().Be(PayrollAdvanceStatus.Pending);
        advance.AppliedToLiquidationId.Should().BeNull();
    }

    [Fact]
    public void Revert_ShouldThrow_WhenNotApplied()
    {
        var advance = CreateAdvance();

        var act = () => advance.Revert();

        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter PayrollAdvanceTests`
Expected: compile error.

- [ ] **Step 3: Implement**

```csharp
namespace eiti.Domain.Payroll;

public sealed record PayrollLiquidationId(Guid Value)
{
    public static PayrollLiquidationId New() => new(Guid.NewGuid());
}
```

```csharp
namespace eiti.Domain.Payroll;

public enum PayrollAdvanceStatus
{
    Pending = 1,
    Applied = 2,
    Cancelled = 3
}
```

```csharp
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;
using eiti.Domain.Users;

namespace eiti.Domain.Payroll;

public sealed class PayrollAdvance : AggregateRoot<PayrollAdvanceId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public EmployeeId EmployeeId { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public DateTime Date { get; private set; }
    public string? Notes { get; private set; }
    public PayrollAdvanceStatus Status { get; private set; }
    public PayrollLiquidationId? AppliedToLiquidationId { get; private set; }
    public UserId CreatedByUserId { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private PayrollAdvance()
    {
    }

    private PayrollAdvance(
        PayrollAdvanceId id,
        CompanyId companyId,
        EmployeeId employeeId,
        decimal amount,
        DateTime date,
        string? notes,
        UserId createdByUserId)
        : base(id)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Advance amount must be greater than zero.", nameof(amount));
        }

        CompanyId = companyId;
        EmployeeId = employeeId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Date = date;
        Notes = NormalizeOptional(notes);
        Status = PayrollAdvanceStatus.Pending;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    public static PayrollAdvance Create(
        CompanyId companyId,
        EmployeeId employeeId,
        decimal amount,
        DateTime date,
        string? notes,
        UserId createdByUserId)
    {
        return new PayrollAdvance(PayrollAdvanceId.New(), companyId, employeeId, amount, date, notes, createdByUserId);
    }

    public void Cancel()
    {
        if (Status != PayrollAdvanceStatus.Pending)
        {
            throw new InvalidOperationException("Only pending advances can be cancelled.");
        }

        Status = PayrollAdvanceStatus.Cancelled;
    }

    public void Apply(PayrollLiquidationId liquidationId)
    {
        if (Status != PayrollAdvanceStatus.Pending)
        {
            throw new InvalidOperationException("Only pending advances can be applied.");
        }

        Status = PayrollAdvanceStatus.Applied;
        AppliedToLiquidationId = liquidationId;
    }

    public void Revert()
    {
        if (Status != PayrollAdvanceStatus.Applied)
        {
            throw new InvalidOperationException("Only applied advances can be reverted.");
        }

        Status = PayrollAdvanceStatus.Pending;
        AppliedToLiquidationId = null;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > 500)
        {
            throw new ArgumentException("Notes cannot exceed 500 characters.", nameof(value));
        }

        return normalized;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter PayrollAdvanceTests`
Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add eiti.Domain/Payroll/PayrollAdvanceId.cs eiti.Domain/Payroll/PayrollAdvanceStatus.cs eiti.Domain/Payroll/PayrollAdvance.cs eiti.Domain/Payroll/PayrollLiquidationId.cs eiti.Tests/PayrollAdvanceTests.cs
git commit -m "feat(payroll): agregar aggregate PayrollAdvance"
```

---

## Task 4: `PayrollLiquidation` aggregate + child lines

**Files:**
- Create: `eiti.Domain/Payroll/PayrollLiquidationStatus.cs`
- Create: `eiti.Domain/Payroll/PayrollPaymentMethod.cs`
- Create: `eiti.Domain/Payroll/PayrollLiquidationDeductionLine.cs`
- Create: `eiti.Domain/Payroll/PayrollLiquidationAdvanceLine.cs`
- Create: `eiti.Domain/Payroll/PayrollLiquidation.cs`
- Test: `eiti.Tests/PayrollLiquidationTests.cs`

**Interfaces:**
- Consumes: `PayrollLiquidationId` (from Task 3), `CompanyId`, `BranchId`, `EmployeeId`.
- Produces: `PayrollLiquidationStatus` (`Pending = 1, Paid = 2, Cancelled = 3`); `PayrollPaymentMethod` (`Cash = 1, Transfer = 2, Other = 3`); `PayrollLiquidationDeductionLine.Create(conceptName, percentage, amount)` + `Id, ConceptName, Percentage, Amount`; `PayrollLiquidationAdvanceLine.Create(payrollAdvanceId, amount)` + `Id, PayrollAdvanceId, Amount`; `PayrollLiquidation` with `Id, CompanyId, EmployeeId, BranchId, PeriodLabel, PeriodStart, PeriodEnd, GrossAmount, Status, PaymentMethod, PaidAt, CashSessionId, CreatedAt, DeductionLines, AdvanceLines, NetAmount`, `Create(...)`, `MarkAsPaid(method, cashSessionId)`, `Cancel()`.

- [ ] **Step 1: Write the failing tests**

```csharp
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;

namespace eiti.Tests;

public sealed class PayrollLiquidationTests
{
    private static PayrollLiquidation CreateLiquidation(
        decimal grossAmount = 500000m,
        IReadOnlyList<PayrollLiquidationDeductionLine>? deductions = null,
        IReadOnlyList<PayrollLiquidationAdvanceLine>? advances = null)
    {
        return PayrollLiquidation.Create(
            CompanyId.New(),
            EmployeeId.New(),
            null,
            "2026-07",
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 31),
            grossAmount,
            deductions ?? [],
            advances ?? []);
    }

    [Fact]
    public void Create_ShouldStartAsPending_WithNetEqualsGross_WhenNoLinesGiven()
    {
        var liquidation = CreateLiquidation(500000m);

        liquidation.Status.Should().Be(PayrollLiquidationStatus.Pending);
        liquidation.NetAmount.Should().Be(500000m);
    }

    [Fact]
    public void Create_ShouldComputeNetAmount_SubtractingDeductionsAndAdvances()
    {
        var deductions = new List<PayrollLiquidationDeductionLine>
        {
            PayrollLiquidationDeductionLine.Create("Jubilacion", 11m, 55000m),
            PayrollLiquidationDeductionLine.Create("Obra social", 3m, 15000m)
        };
        var advances = new List<PayrollLiquidationAdvanceLine>
        {
            PayrollLiquidationAdvanceLine.Create(Guid.NewGuid(), 20000m)
        };

        var liquidation = CreateLiquidation(500000m, deductions, advances);

        liquidation.NetAmount.Should().Be(410000m);
    }

    [Fact]
    public void Create_ShouldThrow_WhenGrossAmountIsZeroOrLess()
    {
        var act = () => CreateLiquidation(0m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsPaid_ShouldRequireCashSessionId_WhenMethodIsCash()
    {
        var liquidation = CreateLiquidation();

        var act = () => liquidation.MarkAsPaid(PayrollPaymentMethod.Cash, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsPaid_ShouldSetPaidAndClearCashSessionId_WhenMethodIsTransfer()
    {
        var liquidation = CreateLiquidation();

        liquidation.MarkAsPaid(PayrollPaymentMethod.Transfer, null);

        liquidation.Status.Should().Be(PayrollLiquidationStatus.Paid);
        liquidation.CashSessionId.Should().BeNull();
        liquidation.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsPaid_ShouldSetCashSessionId_WhenMethodIsCash()
    {
        var liquidation = CreateLiquidation();
        var cashSessionId = Guid.NewGuid();

        liquidation.MarkAsPaid(PayrollPaymentMethod.Cash, cashSessionId);

        liquidation.CashSessionId.Should().Be(cashSessionId);
    }

    [Fact]
    public void MarkAsPaid_ShouldThrow_WhenAlreadyPaid()
    {
        var liquidation = CreateLiquidation();
        liquidation.MarkAsPaid(PayrollPaymentMethod.Transfer, null);

        var act = () => liquidation.MarkAsPaid(PayrollPaymentMethod.Transfer, null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_ShouldSetCancelled()
    {
        var liquidation = CreateLiquidation();

        liquidation.Cancel();

        liquidation.Status.Should().Be(PayrollLiquidationStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldThrow_WhenAlreadyCancelled()
    {
        var liquidation = CreateLiquidation();
        liquidation.Cancel();

        var act = () => liquidation.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter PayrollLiquidationTests`
Expected: compile error.

- [ ] **Step 3: Implement**

```csharp
namespace eiti.Domain.Payroll;

public enum PayrollLiquidationStatus
{
    Pending = 1,
    Paid = 2,
    Cancelled = 3
}
```

```csharp
namespace eiti.Domain.Payroll;

public enum PayrollPaymentMethod
{
    Cash = 1,
    Transfer = 2,
    Other = 3
}
```

```csharp
namespace eiti.Domain.Payroll;

public sealed class PayrollLiquidationDeductionLine
{
    public Guid Id { get; private set; }
    public Guid PayrollLiquidationId { get; private set; }
    public string ConceptName { get; private set; } = string.Empty;
    public decimal Percentage { get; private set; }
    public decimal Amount { get; private set; }

    private PayrollLiquidationDeductionLine()
    {
    }

    private PayrollLiquidationDeductionLine(Guid id, string conceptName, decimal percentage, decimal amount)
    {
        Id = id;
        ConceptName = conceptName.Trim();
        Percentage = percentage;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static PayrollLiquidationDeductionLine Create(string conceptName, decimal percentage, decimal amount)
    {
        return new PayrollLiquidationDeductionLine(Guid.NewGuid(), conceptName, percentage, amount);
    }

    internal void AttachToLiquidation(Guid liquidationId)
    {
        PayrollLiquidationId = liquidationId;
    }
}
```

```csharp
namespace eiti.Domain.Payroll;

public sealed class PayrollLiquidationAdvanceLine
{
    public Guid Id { get; private set; }
    public Guid PayrollLiquidationId { get; private set; }
    public Guid PayrollAdvanceId { get; private set; }
    public decimal Amount { get; private set; }

    private PayrollLiquidationAdvanceLine()
    {
    }

    private PayrollLiquidationAdvanceLine(Guid id, Guid payrollAdvanceId, decimal amount)
    {
        Id = id;
        PayrollAdvanceId = payrollAdvanceId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static PayrollLiquidationAdvanceLine Create(Guid payrollAdvanceId, decimal amount)
    {
        return new PayrollLiquidationAdvanceLine(Guid.NewGuid(), payrollAdvanceId, amount);
    }

    internal void AttachToLiquidation(Guid liquidationId)
    {
        PayrollLiquidationId = liquidationId;
    }
}
```

```csharp
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;

namespace eiti.Domain.Payroll;

public sealed class PayrollLiquidation : AggregateRoot<PayrollLiquidationId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public EmployeeId EmployeeId { get; private set; } = null!;
    public BranchId? BranchId { get; private set; }
    public string PeriodLabel { get; private set; } = string.Empty;
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public decimal GrossAmount { get; private set; }
    public PayrollLiquidationStatus Status { get; private set; }
    public PayrollPaymentMethod? PaymentMethod { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public Guid? CashSessionId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<PayrollLiquidationDeductionLine> _deductionLines = [];
    private readonly List<PayrollLiquidationAdvanceLine> _advanceLines = [];
    public IReadOnlyCollection<PayrollLiquidationDeductionLine> DeductionLines => _deductionLines;
    public IReadOnlyCollection<PayrollLiquidationAdvanceLine> AdvanceLines => _advanceLines;

    public decimal NetAmount => GrossAmount - _deductionLines.Sum(l => l.Amount) - _advanceLines.Sum(l => l.Amount);

    private PayrollLiquidation()
    {
    }

    private PayrollLiquidation(
        PayrollLiquidationId id,
        CompanyId companyId,
        EmployeeId employeeId,
        BranchId? branchId,
        string periodLabel,
        DateTime periodStart,
        DateTime periodEnd,
        decimal grossAmount,
        IReadOnlyList<PayrollLiquidationDeductionLine> deductionLines,
        IReadOnlyList<PayrollLiquidationAdvanceLine> advanceLines)
        : base(id)
    {
        if (grossAmount <= 0)
        {
            throw new ArgumentException("Gross amount must be greater than zero.", nameof(grossAmount));
        }

        if (string.IsNullOrWhiteSpace(periodLabel))
        {
            throw new ArgumentException("Period label cannot be empty.", nameof(periodLabel));
        }

        CompanyId = companyId;
        EmployeeId = employeeId;
        BranchId = branchId;
        PeriodLabel = periodLabel.Trim();
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        GrossAmount = decimal.Round(grossAmount, 2, MidpointRounding.AwayFromZero);
        Status = PayrollLiquidationStatus.Pending;
        CreatedAt = DateTime.UtcNow;

        foreach (var line in deductionLines)
        {
            line.AttachToLiquidation(Id.Value);
            _deductionLines.Add(line);
        }

        foreach (var line in advanceLines)
        {
            line.AttachToLiquidation(Id.Value);
            _advanceLines.Add(line);
        }
    }

    public static PayrollLiquidation Create(
        CompanyId companyId,
        EmployeeId employeeId,
        BranchId? branchId,
        string periodLabel,
        DateTime periodStart,
        DateTime periodEnd,
        decimal grossAmount,
        IReadOnlyList<PayrollLiquidationDeductionLine> deductionLines,
        IReadOnlyList<PayrollLiquidationAdvanceLine> advanceLines)
    {
        return new PayrollLiquidation(
            PayrollLiquidationId.New(),
            companyId,
            employeeId,
            branchId,
            periodLabel,
            periodStart,
            periodEnd,
            grossAmount,
            deductionLines,
            advanceLines);
    }

    public void MarkAsPaid(PayrollPaymentMethod method, Guid? cashSessionId)
    {
        if (Status != PayrollLiquidationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending liquidations can be marked as paid.");
        }

        if (method == PayrollPaymentMethod.Cash && !cashSessionId.HasValue)
        {
            throw new ArgumentException("A cash session is required when paying in cash.", nameof(cashSessionId));
        }

        PaymentMethod = method;
        CashSessionId = method == PayrollPaymentMethod.Cash ? cashSessionId : null;
        Status = PayrollLiquidationStatus.Paid;
        PaidAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == PayrollLiquidationStatus.Cancelled)
        {
            throw new InvalidOperationException("Liquidation is already cancelled.");
        }

        Status = PayrollLiquidationStatus.Cancelled;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter PayrollLiquidationTests`
Expected: 9 passed.

- [ ] **Step 5: Commit**

```bash
git add eiti.Domain/Payroll/PayrollLiquidationStatus.cs eiti.Domain/Payroll/PayrollPaymentMethod.cs eiti.Domain/Payroll/PayrollLiquidationDeductionLine.cs eiti.Domain/Payroll/PayrollLiquidationAdvanceLine.cs eiti.Domain/Payroll/PayrollLiquidation.cs eiti.Tests/PayrollLiquidationTests.cs
git commit -m "feat(payroll): agregar aggregate PayrollLiquidation"
```

---

## Task 5: Cash integration — `CashMovementType`, `CashMovement`, `CashSession.RegisterPayrollExpense`

**Files:**
- Modify: `eiti.Domain/Cash/CashMovementType.cs`
- Modify: `eiti.Domain/Cash/CashReferenceTypes.cs`
- Modify: `eiti.Domain/Cash/CashMovement.cs`
- Modify: `eiti.Domain/Cash/CashSession.cs`
- Test: `eiti.Tests/CashSessionPayrollTests.cs`

**Interfaces:**
- Consumes: `PayrollLiquidationId`/advance `Guid` from Tasks 3-4 (cash layer stores the raw `Guid`, mirroring `SupplierPaymentId: Guid?` on `CashMovement` — not a typed VO, consistent with existing fields).
- Produces: `CashMovementType.PayrollExpense = 15`, `CashMovementType.PayrollExpenseCancellation = 16`, `CashMovementType.PayrollAdvanceExpense = 17`, `CashMovementType.PayrollAdvanceExpenseCancellation = 18`; `CashReferenceTypes.PayrollLiquidation = "PayrollLiquidation"`, `CashReferenceTypes.PayrollAdvance = "PayrollAdvance"`; `CashMovement.PayrollLiquidationId: Guid?`, `CashMovement.PayrollAdvanceId: Guid?`; `CashSession.RegisterPayrollExpense(decimal amount, Guid payrollLiquidationId, UserId createdByUserId)`; `CashSession.RegisterPayrollExpenseCancel(decimal amount, Guid payrollLiquidationId, UserId createdByUserId)`; `CashSession.RegisterPayrollAdvanceExpense(decimal amount, Guid payrollAdvanceId, UserId createdByUserId)`; `CashSession.RegisterPayrollAdvanceExpenseCancel(decimal amount, Guid payrollAdvanceId, UserId createdByUserId)`.

**Design note:** unlike `RegisterSupplierPaymentExpense` (which logs a `Direction.None` movement even for non-cash methods), payroll's `Transfer`/`Other` payment methods create **no `CashMovement` at all** — per the approved spec, those payments never touch a `CashSessionId`. Only `Cash` calls these methods.

- [ ] **Step 1: Write the failing tests**

```csharp
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using FluentAssertions;

namespace eiti.Tests;

public sealed class CashSessionPayrollTests
{
    private static CashSession CreateOpenSession(out UserId userId)
    {
        userId = UserId.New();
        var session = CashSession.Open(
            CashDrawerId.New(),
            CompanyId.New(),
            BranchId.New(),
            userId,
            openingFloat: 100000m);
        return session;
    }

    [Fact]
    public void RegisterPayrollExpense_ShouldAddOutMovement_TaggedWithLiquidationId()
    {
        var session = CreateOpenSession(out var userId);
        var liquidationId = Guid.NewGuid();

        session.RegisterPayrollExpense(50000m, liquidationId, userId);

        var movement = session.Movements.Single(m => m.Type == CashMovementType.PayrollExpense);
        movement.Direction.Should().Be(CashMovementDirection.Out);
        movement.Amount.Should().Be(50000m);
        movement.PayrollLiquidationId.Should().Be(liquidationId);
    }

    [Fact]
    public void RegisterPayrollExpenseCancel_ShouldAddInMovement_ReversingTheExpense()
    {
        var session = CreateOpenSession(out var userId);
        var liquidationId = Guid.NewGuid();
        session.RegisterPayrollExpense(50000m, liquidationId, userId);

        session.RegisterPayrollExpenseCancel(50000m, liquidationId, userId);

        var cancelMovement = session.Movements.Single(m => m.Type == CashMovementType.PayrollExpenseCancellation);
        cancelMovement.Direction.Should().Be(CashMovementDirection.In);
        cancelMovement.PayrollLiquidationId.Should().Be(liquidationId);
    }

    [Fact]
    public void RegisterPayrollAdvanceExpense_ShouldAddOutMovement_TaggedWithAdvanceId()
    {
        var session = CreateOpenSession(out var userId);
        var advanceId = Guid.NewGuid();

        session.RegisterPayrollAdvanceExpense(15000m, advanceId, userId);

        var movement = session.Movements.Single(m => m.Type == CashMovementType.PayrollAdvanceExpense);
        movement.Direction.Should().Be(CashMovementDirection.Out);
        movement.PayrollAdvanceId.Should().Be(advanceId);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter CashSessionPayrollTests`
Expected: compile error (methods/types don't exist). If `CashSession.Open(...)` signature differs from what's assumed here, read `eiti.Domain/Cash/CashSession.cs` for the actual factory signature and adjust the test's setup call only — do not change assertions.

- [ ] **Step 3: Extend `CashMovementType`**

```csharp
namespace eiti.Domain.Cash;

public enum CashMovementType
{
    OpeningFloat = 1,
    SaleIncome = 2,
    CashWithdrawal = 3,
    ManualAdjustment = 4,
    CashTransferOut = 5,
    CashTransferIn = 6,
    SaleCancellation = 7,
    CuentaCorrienteIncome = 8,
    CuentaCorrienteCancellation = 9,
    TransferIncome = 10,
    CardIncome = 11,
    PurchaseExpense = 12,
    CashDeposit = 13,
    PurchasePaymentCancellation = 14,
    PayrollExpense = 15,
    PayrollExpenseCancellation = 16,
    PayrollAdvanceExpense = 17,
    PayrollAdvanceExpenseCancellation = 18
}
```

- [ ] **Step 4: Extend `CashReferenceTypes`**

```csharp
namespace eiti.Domain.Cash;

public static class CashReferenceTypes
{
    public const string Session = "Session";
    public const string Sale = "Sale";
    public const string CuentaCorriente = "CuentaCorriente";
    public const string Withdrawal = "Withdrawal";
    public const string Transfer = "Transfer";
    public const string Purchase = "Purchase";
    public const string SupplierPayment = "SupplierPayment";
    public const string CustomerPayment = "CustomerPayment";
    public const string Deposit = "Deposit";
    public const string PayrollLiquidation = "PayrollLiquidation";
    public const string PayrollAdvance = "PayrollAdvance";
}
```

- [ ] **Step 5: Extend `CashMovement`**

Add two properties next to `SupplierPaymentId` (after line 22 of the current file):

```csharp
    public Guid? PayrollLiquidationId { get; private set; }
    public Guid? PayrollAdvanceId { get; private set; }
```

Add two parameters to the private constructor (after `Guid? customerPaymentId = null` in both the private constructor's parameter list and its body, and in the static `Create` method's parameter list and call):

Private constructor signature becomes:
```csharp
    private CashMovement(
        CashMovementId id,
        CashSessionId cashSessionId,
        CashMovementType type,
        CashMovementDirection direction,
        decimal amount,
        string? referenceType,
        Guid? referenceId,
        string description,
        UserId createdByUserId,
        Guid? ccPaymentGroupId = null,
        Guid? transferCounterpartSessionId = null,
        Guid? originalCashSessionId = null,
        int? paymentMethod = null,
        Guid? saleCcPaymentId = null,
        Guid? supplierPaymentId = null,
        Guid? customerPaymentId = null,
        Guid? payrollLiquidationId = null,
        Guid? payrollAdvanceId = null)
        : base(id)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Cash movement amount must be greater than zero.", nameof(amount));
        }

        CashSessionId = cashSessionId;
        Type = type;
        Direction = direction;
        Amount = amount;
        OccurredAt = DateTime.UtcNow;
        ReferenceType = NormalizeOptional(referenceType, 50, "Reference type");
        ReferenceId = referenceId;
        Description = NormalizeRequired(description, 255, "Description");
        CreatedByUserId = createdByUserId;
        CcPaymentGroupId = ccPaymentGroupId;
        TransferCounterpartSessionId = transferCounterpartSessionId;
        OriginalCashSessionId = originalCashSessionId;
        PaymentMethod = paymentMethod;
        SaleCcPaymentId = saleCcPaymentId;
        SupplierPaymentId = supplierPaymentId;
        CustomerPaymentId = customerPaymentId;
        PayrollLiquidationId = payrollLiquidationId;
        PayrollAdvanceId = payrollAdvanceId;
    }
```

Static `Create` factory becomes:
```csharp
    public static CashMovement Create(
        CashSessionId cashSessionId,
        CashMovementType type,
        CashMovementDirection direction,
        decimal amount,
        string? referenceType,
        Guid? referenceId,
        string description,
        UserId createdByUserId,
        Guid? ccPaymentGroupId = null,
        Guid? transferCounterpartSessionId = null,
        Guid? originalCashSessionId = null,
        int? paymentMethod = null,
        Guid? saleCcPaymentId = null,
        Guid? supplierPaymentId = null,
        Guid? customerPaymentId = null,
        Guid? payrollLiquidationId = null,
        Guid? payrollAdvanceId = null)
    {
        return new CashMovement(
            CashMovementId.New(),
            cashSessionId,
            type,
            direction,
            amount,
            referenceType,
            referenceId,
            description,
            createdByUserId,
            ccPaymentGroupId,
            transferCounterpartSessionId,
            originalCashSessionId,
            paymentMethod,
            saleCcPaymentId,
            supplierPaymentId,
            customerPaymentId,
            payrollLiquidationId,
            payrollAdvanceId);
    }
```

- [ ] **Step 6: Extend `CashSession`**

Add `payrollLiquidationId`/`payrollAdvanceId` parameters to the private `AddMovement` helper (mirrors `supplierPaymentId`):

```csharp
    private void AddMovement(
        CashMovementType type,
        CashMovementDirection direction,
        decimal amount,
        string? referenceType,
        Guid? referenceId,
        string description,
        UserId createdByUserId,
        Guid? originalCashSessionId = null,
        Guid? ccPaymentGroupId = null,
        int? paymentMethod = null,
        Guid? saleCcPaymentId = null,
        Guid? supplierPaymentId = null,
        Guid? customerPaymentId = null,
        Guid? payrollLiquidationId = null,
        Guid? payrollAdvanceId = null)
    {
        _movements.Add(CashMovement.Create(
            Id,
            type,
            direction,
            amount,
            referenceType,
            referenceId,
            description,
            createdByUserId,
            ccPaymentGroupId: ccPaymentGroupId,
            originalCashSessionId: originalCashSessionId,
            paymentMethod: paymentMethod,
            saleCcPaymentId: saleCcPaymentId,
            supplierPaymentId: supplierPaymentId,
            customerPaymentId: customerPaymentId,
            payrollLiquidationId: payrollLiquidationId,
            payrollAdvanceId: payrollAdvanceId));
    }
```

Add these four public methods next to `RegisterSupplierPaymentExpense`/`RegisterSupplierPaymentCancel`:

```csharp
    public void RegisterPayrollExpense(decimal amount, Guid payrollLiquidationId, UserId createdByUserId)
    {
        EnsureOpen();

        if (ExpectedClosingAmount - amount < 0)
        {
            throw new InvalidOperationException("Payroll payment cannot leave a negative expected balance.");
        }

        AddMovement(
            CashMovementType.PayrollExpense,
            CashMovementDirection.Out,
            amount,
            CashReferenceTypes.PayrollLiquidation,
            payrollLiquidationId,
            "Pago de sueldo",
            createdByUserId,
            payrollLiquidationId: payrollLiquidationId);
    }

    public void RegisterPayrollExpenseCancel(decimal amount, Guid payrollLiquidationId, UserId createdByUserId)
    {
        EnsureOpen();

        AddMovement(
            CashMovementType.PayrollExpenseCancellation,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.PayrollLiquidation,
            payrollLiquidationId,
            "Pago de sueldo anulado",
            createdByUserId,
            payrollLiquidationId: payrollLiquidationId);
    }

    public void RegisterPayrollAdvanceExpense(decimal amount, Guid payrollAdvanceId, UserId createdByUserId)
    {
        EnsureOpen();

        if (ExpectedClosingAmount - amount < 0)
        {
            throw new InvalidOperationException("Payroll advance cannot leave a negative expected balance.");
        }

        AddMovement(
            CashMovementType.PayrollAdvanceExpense,
            CashMovementDirection.Out,
            amount,
            CashReferenceTypes.PayrollAdvance,
            payrollAdvanceId,
            "Adelanto de sueldo",
            createdByUserId,
            payrollAdvanceId: payrollAdvanceId);
    }

    public void RegisterPayrollAdvanceExpenseCancel(decimal amount, Guid payrollAdvanceId, UserId createdByUserId)
    {
        EnsureOpen();

        AddMovement(
            CashMovementType.PayrollAdvanceExpenseCancellation,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.PayrollAdvance,
            payrollAdvanceId,
            "Adelanto de sueldo anulado",
            createdByUserId,
            payrollAdvanceId: payrollAdvanceId);
    }
```

- [ ] **Step 7: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter CashSessionPayrollTests`
Expected: 3 passed. If `CashSession.Open` in the test doesn't match the real factory signature, fix only the test's arrangement to match the real one (check `eiti.Domain/Cash/CashSession.cs` for the actual method) — the assertions above must stay as-is.

- [ ] **Step 8: Commit**

```bash
git add eiti.Domain/Cash/CashMovementType.cs eiti.Domain/Cash/CashReferenceTypes.cs eiti.Domain/Cash/CashMovement.cs eiti.Domain/Cash/CashSession.cs eiti.Tests/CashSessionPayrollTests.cs
git commit -m "feat(payroll): integrar pagos de sueldo/adelanto con CashSession"
```

---

## Task 6: EF configurations, `DbSet`s, `CashMovement` columns, migration

**Files:**
- Modify: `eiti.Infrastructure/Persistence/Configurations/EmployeeConfiguration.cs`
- Modify: `eiti.Infrastructure/Persistence/Configurations/CashMovementConfiguration.cs`
- Create: `eiti.Infrastructure/Persistence/Configurations/PayrollDeductionConceptConfiguration.cs`
- Create: `eiti.Infrastructure/Persistence/Configurations/PayrollAdvanceConfiguration.cs`
- Create: `eiti.Infrastructure/Persistence/Configurations/PayrollLiquidationConfiguration.cs`
- Modify: `eiti.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: migration files under `eiti.Infrastructure/Migrations/` (generated by tooling, not hand-written)

**Interfaces:**
- Consumes: all domain types from Tasks 1-5.
- Produces: `ApplicationDbContext.PayrollDeductionConcepts`, `.PayrollAdvances`, `.PayrollLiquidations` `DbSet`s; DB schema for the new tables/columns.

This task has no unit test of its own — its "test" is a clean migration + a successful `dotnet build`. Verification happens in Step 5.

- [ ] **Step 1: Extend `EmployeeConfiguration`**

Add after the `builder.Property(x => x.UpdatedAt)...` line:

```csharp
        builder.Property(x => x.BaseSalary).HasColumnType("decimal(18,2)").IsRequired(false);
        builder.Property(x => x.PayrollPeriodicity).HasConversion<int>().IsRequired(false);
```

- [ ] **Step 2: Extend `CashMovementConfiguration`**

Add next to the `SupplierPaymentId`/`CustomerPaymentId` property + index declarations:

```csharp
        builder.Property(movement => movement.PayrollLiquidationId).IsRequired(false);

        builder.Property(movement => movement.PayrollAdvanceId).IsRequired(false);
```

```csharp
        builder.HasIndex(movement => movement.PayrollLiquidationId);

        builder.HasIndex(movement => movement.PayrollAdvanceId);
```

- [ ] **Step 3: Create the three new configurations**

```csharp
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PayrollDeductionConceptConfiguration : IEntityTypeConfiguration<PayrollDeductionConcept>
{
    public void Configure(EntityTypeBuilder<PayrollDeductionConcept> builder)
    {
        builder.ToTable("PayrollDeductionConcepts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new PayrollDeductionConceptId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Percentage).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.IsActive });
    }
}
```

```csharp
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PayrollAdvanceConfiguration : IEntityTypeConfiguration<PayrollAdvance>
{
    public void Configure(EntityTypeBuilder<PayrollAdvance> builder)
    {
        builder.ToTable("PayrollAdvances");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new PayrollAdvanceId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.EmployeeId).HasConversion(id => id.Value, value => new EmployeeId(value)).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Date).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.AppliedToLiquidationId)
            .HasConversion(id => id!.Value, value => new PayrollLiquidationId(value))
            .IsRequired(false);
        builder.Property(x => x.CreatedByUserId).HasConversion(id => id.Value, value => new UserId(value)).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.Status });
    }
}
```

```csharp
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PayrollLiquidationConfiguration : IEntityTypeConfiguration<PayrollLiquidation>
{
    public void Configure(EntityTypeBuilder<PayrollLiquidation> builder)
    {
        builder.ToTable("PayrollLiquidations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new PayrollLiquidationId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.EmployeeId).HasConversion(id => id.Value, value => new EmployeeId(value)).IsRequired();
        builder.Property(x => x.BranchId).HasConversion(id => id!.Value, value => new BranchId(value)).IsRequired(false);
        builder.Property(x => x.PeriodLabel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PeriodStart).IsRequired();
        builder.Property(x => x.PeriodEnd).IsRequired();
        builder.Property(x => x.GrossAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.PaymentMethod).HasConversion<int?>().IsRequired(false);
        builder.Property(x => x.PaidAt).IsRequired(false);
        builder.Property(x => x.CashSessionId).IsRequired(false);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Ignore(x => x.NetAmount);

        // Único por (empresa, empleado, período) mientras la liquidación no esté cancelada
        // (Status = 3). Mismo patrón de índice único filtrado que SaleTransportAssignment.
        builder.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.PeriodLabel })
            .HasFilter("\"Status\" <> 3")
            .IsUnique();

        builder.HasMany(x => x.DeductionLines)
            .WithOne()
            .HasForeignKey(l => l.PayrollLiquidationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AdvanceLines)
            .WithOne()
            .HasForeignKey(l => l.PayrollLiquidationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.DeductionLines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.AdvanceLines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PayrollLiquidationDeductionLineConfiguration : IEntityTypeConfiguration<PayrollLiquidationDeductionLine>
{
    public void Configure(EntityTypeBuilder<PayrollLiquidationDeductionLine> builder)
    {
        builder.ToTable("PayrollLiquidationDeductionLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PayrollLiquidationId).IsRequired();
        builder.Property(x => x.ConceptName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Percentage).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
    }
}

public sealed class PayrollLiquidationAdvanceLineConfiguration : IEntityTypeConfiguration<PayrollLiquidationAdvanceLine>
{
    public void Configure(EntityTypeBuilder<PayrollLiquidationAdvanceLine> builder)
    {
        builder.ToTable("PayrollLiquidationAdvanceLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PayrollLiquidationId).IsRequired();
        builder.Property(x => x.PayrollAdvanceId).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
    }
}
```

- [ ] **Step 4: Add `DbSet`s to `ApplicationDbContext`**

Add next to `public DbSet<Bank> Banks => Set<Bank>();`:

```csharp
    public DbSet<eiti.Domain.Payroll.PayrollDeductionConcept> PayrollDeductionConcepts => Set<eiti.Domain.Payroll.PayrollDeductionConcept>();
    public DbSet<eiti.Domain.Payroll.PayrollAdvance> PayrollAdvances => Set<eiti.Domain.Payroll.PayrollAdvance>();
    public DbSet<eiti.Domain.Payroll.PayrollLiquidation> PayrollLiquidations => Set<eiti.Domain.Payroll.PayrollLiquidation>();
```

(Use the fully qualified `eiti.Domain.Payroll.X` form only if `ApplicationDbContext.cs` doesn't already have a `using eiti.Domain.Payroll;` — otherwise add the `using` and drop the prefix, matching the file's existing style.)

- [ ] **Step 5: Build, then generate and apply the migration**

Run: `dotnet build eiti.Infrastructure/eiti.Infrastructure.csproj`
Expected: 0 errors. If the API process is running and locks DLLs (see `CLAUDE.md` Infrastructure Rules), stop it first.

Run: `dotnet ef migrations add AddPayrollModule --project eiti.Infrastructure --startup-project eiti.Api --output-dir Migrations`
Expected: a new `<timestamp>_AddPayrollModule.cs` + `.Designer.cs` appear under `eiti.Infrastructure/Migrations/`, and `ApplicationDbContextModelSnapshot.cs` is updated. Open the generated migration and confirm it creates `PayrollDeductionConcepts`, `PayrollAdvances`, `PayrollLiquidations`, `PayrollLiquidationDeductionLines`, `PayrollLiquidationAdvanceLines`, adds `BaseSalary`/`PayrollPeriodicity` to `Employees`, and adds `PayrollLiquidationId`/`PayrollAdvanceId` to `CashMovements`. If the migration is empty or missing any of these, the configuration in Step 3/4 wasn't picked up — re-check the `DbSet` registrations before re-running.

Do **not** run `dotnet ef database update` yet — that happens automatically on next deploy via `Database.Migrate()` per `CLAUDE.md`, or you can run it locally against your dev DB if you're testing interactively.

- [ ] **Step 6: Commit**

```bash
git add eiti.Infrastructure/Persistence/Configurations/EmployeeConfiguration.cs eiti.Infrastructure/Persistence/Configurations/CashMovementConfiguration.cs eiti.Infrastructure/Persistence/Configurations/PayrollDeductionConceptConfiguration.cs eiti.Infrastructure/Persistence/Configurations/PayrollAdvanceConfiguration.cs eiti.Infrastructure/Persistence/Configurations/PayrollLiquidationConfiguration.cs eiti.Infrastructure/Persistence/ApplicationDbContext.cs eiti.Infrastructure/Migrations/
git commit -m "feat(payroll): configuraciones EF + migracion AddPayrollModule"
```

---

## Task 7: Repositories + DI registration

**Files:**
- Create: `eiti.Application/Abstractions/Repositories/IPayrollDeductionConceptRepository.cs`
- Create: `eiti.Application/Abstractions/Repositories/IPayrollAdvanceRepository.cs`
- Create: `eiti.Application/Abstractions/Repositories/IPayrollLiquidationRepository.cs`
- Create: `eiti.Infrastructure/Persistence/Repositories/PayrollDeductionConceptRepository.cs`
- Create: `eiti.Infrastructure/Persistence/Repositories/PayrollAdvanceRepository.cs`
- Create: `eiti.Infrastructure/Persistence/Repositories/PayrollLiquidationRepository.cs`
- Modify: `eiti.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Produces: the three repository interfaces below, consumed directly by Tasks 9-15's handlers.

- [ ] **Step 1: Interfaces**

```csharp
using eiti.Domain.Companies;
using eiti.Domain.Payroll;

namespace eiti.Application.Abstractions.Repositories;

public interface IPayrollDeductionConceptRepository
{
    Task AddAsync(PayrollDeductionConcept concept, CancellationToken cancellationToken = default);
    Task<PayrollDeductionConcept?> GetByIdAsync(PayrollDeductionConceptId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollDeductionConcept>> ListByCompanyAsync(CompanyId companyId, bool activeOnly, CancellationToken cancellationToken = default);
}
```

```csharp
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;

namespace eiti.Application.Abstractions.Repositories;

public interface IPayrollAdvanceRepository
{
    Task AddAsync(PayrollAdvance advance, CancellationToken cancellationToken = default);
    Task<PayrollAdvance?> GetByIdAsync(PayrollAdvanceId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollAdvance>> ListByCompanyAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        PayrollAdvanceStatus? status,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollAdvance>> ListPendingByEmployeeAsync(CompanyId companyId, EmployeeId employeeId, CancellationToken cancellationToken = default);
}
```

```csharp
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;

namespace eiti.Application.Abstractions.Repositories;

public interface IPayrollLiquidationRepository
{
    Task AddAsync(PayrollLiquidation liquidation, CancellationToken cancellationToken = default);
    Task<PayrollLiquidation?> GetByIdAsync(PayrollLiquidationId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<bool> ExistsForPeriodAsync(CompanyId companyId, EmployeeId employeeId, string periodLabel, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollLiquidation>> ListAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        string? periodLabel,
        PayrollLiquidationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> CountAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        string? periodLabel,
        PayrollLiquidationStatus? status,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Implementations**

```csharp
using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using eiti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class PayrollDeductionConceptRepository : IPayrollDeductionConceptRepository
{
    private readonly ApplicationDbContext _context;

    public PayrollDeductionConceptRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PayrollDeductionConcept concept, CancellationToken cancellationToken = default)
    {
        await _context.PayrollDeductionConcepts.AddAsync(concept, cancellationToken);
    }

    public async Task<PayrollDeductionConcept?> GetByIdAsync(PayrollDeductionConceptId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollDeductionConcepts
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollDeductionConcept>> ListByCompanyAsync(CompanyId companyId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollDeductionConcepts.Where(x => x.CompanyId == companyId);

        if (activeOnly)
            query = query.Where(x => x.IsActive);

        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }
}
```

```csharp
using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using eiti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class PayrollAdvanceRepository : IPayrollAdvanceRepository
{
    private readonly ApplicationDbContext _context;

    public PayrollAdvanceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PayrollAdvance advance, CancellationToken cancellationToken = default)
    {
        await _context.PayrollAdvances.AddAsync(advance, cancellationToken);
    }

    public async Task<PayrollAdvance?> GetByIdAsync(PayrollAdvanceId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollAdvances
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollAdvance>> ListByCompanyAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        PayrollAdvanceStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollAdvances.Where(x => x.CompanyId == companyId);

        if (employeeId is not null)
            query = query.Where(x => x.EmployeeId == employeeId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await query.OrderByDescending(x => x.Date).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollAdvance>> ListPendingByEmployeeAsync(CompanyId companyId, EmployeeId employeeId, CancellationToken cancellationToken = default)
    {
        // Tracked (sin AsNoTracking): el batch de liquidacion marca estos adelantos como
        // Applied en el mismo SaveChanges que crea la liquidacion.
        return await _context.PayrollAdvances
            .Where(x => x.CompanyId == companyId && x.EmployeeId == employeeId && x.Status == PayrollAdvanceStatus.Pending)
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);
    }
}
```

```csharp
using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using eiti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class PayrollLiquidationRepository : IPayrollLiquidationRepository
{
    private readonly ApplicationDbContext _context;

    public PayrollLiquidationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PayrollLiquidation liquidation, CancellationToken cancellationToken = default)
    {
        await _context.PayrollLiquidations.AddAsync(liquidation, cancellationToken);
    }

    public async Task<PayrollLiquidation?> GetByIdAsync(PayrollLiquidationId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollLiquidations
            .Include(x => x.DeductionLines)
            .Include(x => x.AdvanceLines)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);
    }

    public async Task<bool> ExistsForPeriodAsync(CompanyId companyId, EmployeeId employeeId, string periodLabel, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollLiquidations.AnyAsync(
            x => x.CompanyId == companyId
                && x.EmployeeId == employeeId
                && x.PeriodLabel == periodLabel
                && x.Status != PayrollLiquidationStatus.Cancelled,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollLiquidation>> ListAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        string? periodLabel,
        PayrollLiquidationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(companyId, employeeId, periodLabel, status)
            .Include(x => x.DeductionLines)
            .Include(x => x.AdvanceLines)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        string? periodLabel,
        PayrollLiquidationStatus? status,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(companyId, employeeId, periodLabel, status).CountAsync(cancellationToken);
    }

    private IQueryable<PayrollLiquidation> BuildQuery(
        CompanyId companyId,
        EmployeeId? employeeId,
        string? periodLabel,
        PayrollLiquidationStatus? status)
    {
        var query = _context.PayrollLiquidations.Where(x => x.CompanyId == companyId);

        if (employeeId is not null)
            query = query.Where(x => x.EmployeeId == employeeId);

        if (!string.IsNullOrWhiteSpace(periodLabel))
            query = query.Where(x => x.PeriodLabel == periodLabel);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return query;
    }
}
```

- [ ] **Step 3: Register in DI**

Add to `eiti.Infrastructure/DependencyInjection.cs`, next to `services.AddScoped<IPurchaseRepository, PurchaseRepository>();`:

```csharp
        services.AddScoped<IPayrollDeductionConceptRepository, PayrollDeductionConceptRepository>();
        services.AddScoped<IPayrollAdvanceRepository, PayrollAdvanceRepository>();
        services.AddScoped<IPayrollLiquidationRepository, PayrollLiquidationRepository>();
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build eiti.Infrastructure/eiti.Infrastructure.csproj`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add eiti.Application/Abstractions/Repositories/IPayrollDeductionConceptRepository.cs eiti.Application/Abstractions/Repositories/IPayrollAdvanceRepository.cs eiti.Application/Abstractions/Repositories/IPayrollLiquidationRepository.cs eiti.Infrastructure/Persistence/Repositories/PayrollDeductionConceptRepository.cs eiti.Infrastructure/Persistence/Repositories/PayrollAdvanceRepository.cs eiti.Infrastructure/Persistence/Repositories/PayrollLiquidationRepository.cs eiti.Infrastructure/DependencyInjection.cs
git commit -m "feat(payroll): repositorios de payroll + registro DI"
```

---

## Task 8: Permissions — `PermissionCodes`, `PermissionCatalog`, `RoleCatalog`

**Files:**
- Modify: `eiti.Application/Common/Authorization/PermissionCodes.cs`
- Modify: `eiti.Application/Common/Authorization/PermissionCatalog.cs`
- Modify: `eiti.Application/Common/Authorization/RoleCatalog.cs`

**Interfaces:**
- Produces: `PermissionCodes.PayrollManage`, `PermissionCodes.PayrollLiquidationsGenerate`, `PermissionCodes.PayrollLiquidationsPay`, `PermissionCodes.PayrollAdvancesManage` — consumed by every command in Tasks 9-14 via `IRequirePermissions`.

No unit test — this is config data. Verified by a build (Step 4) and later by the handler tests in Tasks 9-14, which reference these constants directly (a typo here would fail those builds).

- [ ] **Step 1: Add to `PermissionCodes.cs`**

Add near the bottom of the class, following the existing grouping style (a blank line + a short comment if the group needs one):

```csharp
    public const string PayrollManage = "payroll.manage";
    public const string PayrollLiquidationsGenerate = "payroll.liquidations.generate";
    public const string PayrollLiquidationsPay = "payroll.liquidations.pay";
    public const string PayrollAdvancesManage = "payroll.advances.manage";
```

- [ ] **Step 2: Add to `PermissionCatalog.All`**

Add these four lines to the `HashSet<string>` initializer (anywhere in the list, matching the existing one-per-line style):

```csharp
        PermissionCodes.PayrollManage,
        PermissionCodes.PayrollLiquidationsGenerate,
        PermissionCodes.PayrollLiquidationsPay,
        PermissionCodes.PayrollAdvancesManage,
```

- [ ] **Step 3: Assign to `Owner` and `Admin` in `RoleCatalog.cs`**

Add these four lines to **both** the `Owner` and `Admin` `RoleDefinition` permission arrays (not `Seller`/`Cashier` — payroll is an admin-level concern):

```csharp
                PermissionCodes.PayrollManage,
                PermissionCodes.PayrollLiquidationsGenerate,
                PermissionCodes.PayrollLiquidationsPay,
                PermissionCodes.PayrollAdvancesManage,
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build eiti.Application/eiti.Application.csproj`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add eiti.Application/Common/Authorization/PermissionCodes.cs eiti.Application/Common/Authorization/PermissionCatalog.cs eiti.Application/Common/Authorization/RoleCatalog.cs
git commit -m "feat(payroll): permisos de payroll (manage, generate, pay, advances)"
```

**Reminder for whoever executes this plan:** after deploying, the running API process must restart — `PermissionCatalog.All` is read into memory once at startup (per `.claude/rules/lessons.md`).

---

## Task 9: Deduction concepts CRUD (Create, Update, Deactivate, List)

**Files:**
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Commands/CreateDeductionConcept/CreateDeductionConceptCommand.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Commands/CreateDeductionConcept/CreateDeductionConceptValidator.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Commands/CreateDeductionConcept/CreateDeductionConceptErrors.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Commands/CreateDeductionConcept/CreateDeductionConceptHandler.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Commands/UpdateDeductionConcept/UpdateDeductionConceptCommand.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Commands/UpdateDeductionConcept/UpdateDeductionConceptValidator.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Commands/UpdateDeductionConcept/UpdateDeductionConceptErrors.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Commands/UpdateDeductionConcept/UpdateDeductionConceptHandler.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Commands/SetDeductionConceptActive/SetDeductionConceptActiveCommand.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Commands/SetDeductionConceptActive/SetDeductionConceptActiveErrors.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Commands/SetDeductionConceptActive/SetDeductionConceptActiveHandler.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Queries/ListDeductionConcepts/ListDeductionConceptsQuery.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/Queries/ListDeductionConcepts/ListDeductionConceptsHandler.cs`
- Create: `eiti.Application/Features/Payroll/DeductionConcepts/DeductionConceptResponse.cs`
- Test: `eiti.Tests/DeductionConceptHandlersTests.cs`

**Interfaces:**
- Consumes: `IPayrollDeductionConceptRepository` (Task 7), `PermissionCodes.PayrollManage` (Task 8), `PayrollDeductionConcept` (Task 2).
- Produces: `DeductionConceptResponse(Guid Id, string Name, decimal Percentage, bool IsActive)`, consumed later by Task 12 (batch generation) and the frontend.

- [ ] **Step 1: Response + Query + Commands (no logic yet — these are records)**

```csharp
namespace eiti.Application.Features.Payroll.DeductionConcepts;

public sealed record DeductionConceptResponse(Guid Id, string Name, decimal Percentage, bool IsActive);
```

```csharp
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.DeductionConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;

public sealed record CreateDeductionConceptCommand(string Name, decimal Percentage)
    : IRequest<Result<DeductionConceptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
```

```csharp
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.DeductionConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.UpdateDeductionConcept;

public sealed record UpdateDeductionConceptCommand(Guid Id, string Name, decimal Percentage)
    : IRequest<Result<DeductionConceptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
```

```csharp
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.DeductionConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.SetDeductionConceptActive;

public sealed record SetDeductionConceptActiveCommand(Guid Id, bool IsActive)
    : IRequest<Result<DeductionConceptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
```

```csharp
using eiti.Application.Common;
using eiti.Application.Features.Payroll.DeductionConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Queries.ListDeductionConcepts;

public sealed record ListDeductionConceptsQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<DeductionConceptResponse>>>;
```

- [ ] **Step 2: Write the failing handler tests**

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.SetDeductionConceptActive;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.UpdateDeductionConcept;
using eiti.Application.Features.Payroll.DeductionConcepts.Queries.ListDeductionConcepts;
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class DeductionConceptHandlersTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        return user;
    }

    [Fact]
    public async Task CreateHandler_ShouldPersistConcept_AndReturnResponse()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var repository = new Mock<IPayrollDeductionConceptRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        PayrollDeductionConcept? persisted = null;
        repository
            .Setup(r => r.AddAsync(It.IsAny<PayrollDeductionConcept>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollDeductionConcept, CancellationToken>((c, _) => persisted = c)
            .Returns(Task.CompletedTask);

        var handler = new CreateDeductionConceptHandler(user.Object, repository.Object, unitOfWork.Object);

        var result = await handler.Handle(new CreateDeductionConceptCommand("Jubilacion", 11m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Jubilacion");
        result.Value.Percentage.Should().Be(11m);
    }

    [Fact]
    public async Task UpdateHandler_ShouldFail_WhenConceptNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var repository = new Mock<IPayrollDeductionConceptRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollDeductionConceptId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollDeductionConcept?)null);

        var handler = new UpdateDeductionConceptHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new UpdateDeductionConceptCommand(Guid.NewGuid(), "Nuevo nombre", 5m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveHandler_ShouldDeactivate_WhenIsActiveFalse()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var concept = PayrollDeductionConcept.Create(companyId, "ART", 3m);
        var repository = new Mock<IPayrollDeductionConceptRepository>();
        repository
            .Setup(r => r.GetByIdAsync(concept.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(concept);

        var handler = new SetDeductionConceptActiveHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new SetDeductionConceptActiveCommand(concept.Id.Value, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ListHandler_ShouldReturnMappedItems()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var concept = PayrollDeductionConcept.Create(companyId, "Obra social", 3m);
        var repository = new Mock<IPayrollDeductionConceptRepository>();
        repository
            .Setup(r => r.ListByCompanyAsync(companyId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollDeductionConcept> { concept });

        var handler = new ListDeductionConceptsHandler(user.Object, repository.Object);

        var result = await handler.Handle(new ListDeductionConceptsQuery(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(x => x.Name == "Obra social");
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter DeductionConceptHandlersTests`
Expected: compile error (handlers don't exist).

- [ ] **Step 4: Implement validators, errors, and handlers**

```csharp
using FluentValidation;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;

public sealed class CreateDeductionConceptValidator : AbstractValidator<CreateDeductionConceptCommand>
{
    public CreateDeductionConceptValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Percentage).InclusiveBetween(0, 100);
    }
}
```

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;

public static class CreateDeductionConceptErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.DeductionConcepts.Create.Unauthorized",
        "Authentication is required.");
}
```

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.DeductionConcepts;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;

public sealed class CreateDeductionConceptHandler : IRequestHandler<CreateDeductionConceptCommand, Result<DeductionConceptResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollDeductionConceptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDeductionConceptHandler(
        ICurrentUserService currentUserService,
        IPayrollDeductionConceptRepository repository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DeductionConceptResponse>> Handle(CreateDeductionConceptCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<DeductionConceptResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<DeductionConceptResponse>.Failure(CreateDeductionConceptErrors.Unauthorized);

        var concept = PayrollDeductionConcept.Create(_currentUserService.CompanyId!, request.Name, request.Percentage);

        await _repository.AddAsync(concept, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<DeductionConceptResponse>.Success(
            new DeductionConceptResponse(concept.Id.Value, concept.Name, concept.Percentage, concept.IsActive));
    }
}
```

```csharp
using FluentValidation;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.UpdateDeductionConcept;

public sealed class UpdateDeductionConceptValidator : AbstractValidator<UpdateDeductionConceptCommand>
{
    public UpdateDeductionConceptValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Percentage).InclusiveBetween(0, 100);
    }
}
```

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.UpdateDeductionConcept;

public static class UpdateDeductionConceptErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.DeductionConcepts.Update.NotFound",
        "The requested deduction concept was not found.");
}
```

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.DeductionConcepts;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.UpdateDeductionConcept;

public sealed class UpdateDeductionConceptHandler : IRequestHandler<UpdateDeductionConceptCommand, Result<DeductionConceptResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollDeductionConceptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDeductionConceptHandler(
        ICurrentUserService currentUserService,
        IPayrollDeductionConceptRepository repository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DeductionConceptResponse>> Handle(UpdateDeductionConceptCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<DeductionConceptResponse>.Failure(authCheck.Error);

        var concept = await _repository.GetByIdAsync(new PayrollDeductionConceptId(request.Id), _currentUserService.CompanyId!, cancellationToken);
        if (concept is null)
            return Result<DeductionConceptResponse>.Failure(UpdateDeductionConceptErrors.NotFound);

        concept.Update(request.Name, request.Percentage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<DeductionConceptResponse>.Success(
            new DeductionConceptResponse(concept.Id.Value, concept.Name, concept.Percentage, concept.IsActive));
    }
}
```

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.SetDeductionConceptActive;

public static class SetDeductionConceptActiveErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.DeductionConcepts.SetActive.NotFound",
        "The requested deduction concept was not found.");
}
```

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.DeductionConcepts;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.SetDeductionConceptActive;

public sealed class SetDeductionConceptActiveHandler : IRequestHandler<SetDeductionConceptActiveCommand, Result<DeductionConceptResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollDeductionConceptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetDeductionConceptActiveHandler(
        ICurrentUserService currentUserService,
        IPayrollDeductionConceptRepository repository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DeductionConceptResponse>> Handle(SetDeductionConceptActiveCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<DeductionConceptResponse>.Failure(authCheck.Error);

        var concept = await _repository.GetByIdAsync(new PayrollDeductionConceptId(request.Id), _currentUserService.CompanyId!, cancellationToken);
        if (concept is null)
            return Result<DeductionConceptResponse>.Failure(SetDeductionConceptActiveErrors.NotFound);

        if (request.IsActive)
            concept.Activate();
        else
            concept.Deactivate();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<DeductionConceptResponse>.Success(
            new DeductionConceptResponse(concept.Id.Value, concept.Name, concept.Percentage, concept.IsActive));
    }
}
```

```csharp
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.DeductionConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Queries.ListDeductionConcepts;

public sealed class ListDeductionConceptsHandler : IRequestHandler<ListDeductionConceptsQuery, Result<IReadOnlyList<DeductionConceptResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollDeductionConceptRepository _repository;

    public ListDeductionConceptsHandler(ICurrentUserService currentUserService, IPayrollDeductionConceptRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<DeductionConceptResponse>>> Handle(ListDeductionConceptsQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<DeductionConceptResponse>>.Failure(authCheck.Error);

        var concepts = await _repository.ListByCompanyAsync(_currentUserService.CompanyId!, request.ActiveOnly, cancellationToken);

        IReadOnlyList<DeductionConceptResponse> items = concepts
            .Select(c => new DeductionConceptResponse(c.Id.Value, c.Name, c.Percentage, c.IsActive))
            .ToList();

        return Result<IReadOnlyList<DeductionConceptResponse>>.Success(items);
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter DeductionConceptHandlersTests`
Expected: 4 passed.

- [ ] **Step 6: Commit**

```bash
git add eiti.Application/Features/Payroll/DeductionConcepts/ eiti.Tests/DeductionConceptHandlersTests.cs
git commit -m "feat(payroll): CRUD de conceptos de descuento"
```

---

## Task 10: `SetEmployeePayrollConfig` command

**Files:**
- Create: `eiti.Application/Features/Payroll/Employees/Commands/SetEmployeePayrollConfig/SetEmployeePayrollConfigCommand.cs`
- Create: `eiti.Application/Features/Payroll/Employees/Commands/SetEmployeePayrollConfig/SetEmployeePayrollConfigValidator.cs`
- Create: `eiti.Application/Features/Payroll/Employees/Commands/SetEmployeePayrollConfig/SetEmployeePayrollConfigErrors.cs`
- Create: `eiti.Application/Features/Payroll/Employees/Commands/SetEmployeePayrollConfig/SetEmployeePayrollConfigHandler.cs`
- Create: `eiti.Application/Features/Payroll/Employees/Commands/SetEmployeePayrollConfig/SetEmployeePayrollConfigResponse.cs`
- Test: `eiti.Tests/SetEmployeePayrollConfigHandlerTests.cs`

**Interfaces:**
- Consumes: `IEmployeeRepository` (existing), `Employee.SetPayrollConfig` (Task 1).
- Produces: `SetEmployeePayrollConfigResponse(Guid EmployeeId, decimal? BaseSalary, int? PayrollPeriodicity)`.

**Design note:** deliberately a separate command from `UpdateEmployeeCommand` (in `EmployeeFeature.cs`) rather than adding fields to it — `UpdateEmployeeCommand` is used by non-payroll flows (e.g. driver profile edits) and this keeps that surface untouched.

- [ ] **Step 1: Write the failing test**

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class SetEmployeePayrollConfigHandlerTests
{
    [Fact]
    public async Task Handle_ShouldSetBaseSalaryAndPeriodicity_WhenEmployeeExists()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Seller);

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);

        var repository = new Mock<IEmployeeRepository>();
        repository
            .Setup(r => r.GetByIdAsync(employee.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var handler = new SetEmployeePayrollConfigHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new SetEmployeePayrollConfigCommand(employee.Id.Value, 500000m, (int)PayrollPeriodicity.Monthly),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BaseSalary.Should().Be(500000m);
        result.Value.PayrollPeriodicity.Should().Be((int)PayrollPeriodicity.Monthly);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEmployeeNotFound()
    {
        var companyId = CompanyId.New();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);

        var repository = new Mock<IEmployeeRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<EmployeeId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var handler = new SetEmployeePayrollConfigHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new SetEmployeePayrollConfigCommand(Guid.NewGuid(), 500000m, (int)PayrollPeriodicity.Monthly),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPeriodicityInvalid()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Seller);

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);

        var repository = new Mock<IEmployeeRepository>();
        repository
            .Setup(r => r.GetByIdAsync(employee.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var handler = new SetEmployeePayrollConfigHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new SetEmployeePayrollConfigCommand(employee.Id.Value, 500000m, 999),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter SetEmployeePayrollConfigHandlerTests`
Expected: compile error.

- [ ] **Step 3: Implement**

```csharp
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;
using MediatR;

namespace eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;

public sealed record SetEmployeePayrollConfigCommand(Guid EmployeeId, decimal? BaseSalary, int? PayrollPeriodicity)
    : IRequest<Result<SetEmployeePayrollConfigResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
```

```csharp
namespace eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;

public sealed record SetEmployeePayrollConfigResponse(Guid EmployeeId, decimal? BaseSalary, int? PayrollPeriodicity);
```

```csharp
using FluentValidation;

namespace eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;

public sealed class SetEmployeePayrollConfigValidator : AbstractValidator<SetEmployeePayrollConfigCommand>
{
    public SetEmployeePayrollConfigValidator()
    {
        RuleFor(x => x.BaseSalary).GreaterThanOrEqualTo(0).When(x => x.BaseSalary.HasValue);
        RuleFor(x => x.PayrollPeriodicity)
            .Must(value => Enum.IsDefined(typeof(eiti.Domain.Employees.PayrollPeriodicity), value!.Value))
            .When(x => x.PayrollPeriodicity.HasValue)
            .WithMessage("The selected payroll periodicity is invalid.");
    }
}
```

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;

public static class SetEmployeePayrollConfigErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Employees.SetPayrollConfig.NotFound",
        "The requested employee was not found.");
}
```

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Employees;
using MediatR;

namespace eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;

public sealed class SetEmployeePayrollConfigHandler : IRequestHandler<SetEmployeePayrollConfigCommand, Result<SetEmployeePayrollConfigResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetEmployeePayrollConfigHandler(
        ICurrentUserService currentUserService,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SetEmployeePayrollConfigResponse>> Handle(SetEmployeePayrollConfigCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<SetEmployeePayrollConfigResponse>.Failure(authCheck.Error);

        var employee = await _employeeRepository.GetByIdAsync(new EmployeeId(request.EmployeeId), _currentUserService.CompanyId!, cancellationToken);
        if (employee is null)
            return Result<SetEmployeePayrollConfigResponse>.Failure(SetEmployeePayrollConfigErrors.NotFound);

        var periodicity = request.PayrollPeriodicity.HasValue
            ? (PayrollPeriodicity)request.PayrollPeriodicity.Value
            : (PayrollPeriodicity?)null;

        try
        {
            employee.SetPayrollConfig(request.BaseSalary, periodicity);
        }
        catch (ArgumentException ex)
        {
            return Result<SetEmployeePayrollConfigResponse>.Failure(Error.Validation("Payroll.Employees.SetPayrollConfig.InvalidInput", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SetEmployeePayrollConfigResponse>.Success(
            new SetEmployeePayrollConfigResponse(employee.Id.Value, employee.BaseSalary, (int?)employee.PayrollPeriodicity));
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter SetEmployeePayrollConfigHandlerTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add eiti.Application/Features/Payroll/Employees/ eiti.Tests/SetEmployeePayrollConfigHandlerTests.cs
git commit -m "feat(payroll): comando para configurar sueldo base y periodicidad del empleado"
```

---

## Task 11: Advances — Create, Cancel, List

**Files:**
- Create: `eiti.Application/Features/Payroll/Advances/PayrollAdvanceResponse.cs`
- Create: `eiti.Application/Features/Payroll/Advances/Commands/CreatePayrollAdvance/CreatePayrollAdvanceCommand.cs`
- Create: `eiti.Application/Features/Payroll/Advances/Commands/CreatePayrollAdvance/CreatePayrollAdvanceValidator.cs`
- Create: `eiti.Application/Features/Payroll/Advances/Commands/CreatePayrollAdvance/CreatePayrollAdvanceErrors.cs`
- Create: `eiti.Application/Features/Payroll/Advances/Commands/CreatePayrollAdvance/CreatePayrollAdvanceHandler.cs`
- Create: `eiti.Application/Features/Payroll/Advances/Commands/CancelPayrollAdvance/CancelPayrollAdvanceCommand.cs`
- Create: `eiti.Application/Features/Payroll/Advances/Commands/CancelPayrollAdvance/CancelPayrollAdvanceErrors.cs`
- Create: `eiti.Application/Features/Payroll/Advances/Commands/CancelPayrollAdvance/CancelPayrollAdvanceHandler.cs`
- Create: `eiti.Application/Features/Payroll/Advances/Queries/ListPayrollAdvances/ListPayrollAdvancesQuery.cs`
- Create: `eiti.Application/Features/Payroll/Advances/Queries/ListPayrollAdvances/ListPayrollAdvancesHandler.cs`
- Test: `eiti.Tests/PayrollAdvanceHandlersTests.cs`

**Interfaces:**
- Consumes: `IPayrollAdvanceRepository`, `IEmployeeRepository`, `ICashDrawerRepository`, `ICashSessionRepository` (existing), `CashDrawerAccessPolicy.EnsureCanAccessDrawerAsync` (existing), `PayrollPaymentMethod` (Task 4), `CashSession.RegisterPayrollAdvanceExpense` / `RegisterPayrollAdvanceExpenseCancel` (Task 5).
- Produces: `PayrollAdvanceResponse(Guid Id, Guid EmployeeId, decimal Amount, DateTime Date, string? Notes, int Status, Guid? AppliedToLiquidationId)`, consumed by Task 12 (batch generation reads `PayrollAdvance` entities directly, not this response — this response is API-facing only) and the frontend.

- [ ] **Step 1: Response + records**

```csharp
namespace eiti.Application.Features.Payroll.Advances;

public sealed record PayrollAdvanceResponse(
    Guid Id,
    Guid EmployeeId,
    decimal Amount,
    DateTime Date,
    string? Notes,
    int Status,
    Guid? AppliedToLiquidationId);
```

```csharp
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Advances;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;

public sealed record CreatePayrollAdvanceCommand(
    Guid EmployeeId,
    decimal Amount,
    DateTime Date,
    string? Notes,
    int PaymentMethod,
    Guid? CashSessionId) : IRequest<Result<PayrollAdvanceResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollAdvancesManage];
}
```

```csharp
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Advances;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Commands.CancelPayrollAdvance;

public sealed record CancelPayrollAdvanceCommand(Guid Id) : IRequest<Result<PayrollAdvanceResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollAdvancesManage];
}
```

```csharp
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Advances;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Queries.ListPayrollAdvances;

public sealed record ListPayrollAdvancesQuery(Guid? EmployeeId, int? Status) : IRequest<Result<IReadOnlyList<PayrollAdvanceResponse>>>;
```

- [ ] **Step 2: Write the failing handler tests**

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Advances.Commands.CancelPayrollAdvance;
using eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;
using eiti.Application.Features.Payroll.Advances.Queries.ListPayrollAdvances;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class PayrollAdvanceHandlersTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        user.SetupGet(u => u.UserId).Returns(eiti.Domain.Users.UserId.New());
        return user;
    }

    [Fact]
    public async Task CreateHandler_ShouldPersistAdvance_WhenPaymentMethodIsTransfer()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Seller);
        var user = MockUser(companyId);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(employee.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var advanceRepository = new Mock<IPayrollAdvanceRepository>();
        PayrollAdvance? persisted = null;
        advanceRepository
            .Setup(r => r.AddAsync(It.IsAny<PayrollAdvance>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollAdvance, CancellationToken>((a, _) => persisted = a)
            .Returns(Task.CompletedTask);

        var handler = new CreatePayrollAdvanceHandler(
            user.Object,
            advanceRepository.Object,
            employeeRepository.Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreatePayrollAdvanceCommand(employee.Id.Value, 15000m, DateTime.UtcNow, "Adelanto", (int)PayrollPaymentMethod.Transfer, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Amount.Should().Be(15000m);
    }

    [Fact]
    public async Task CreateHandler_ShouldFail_WhenCashWithoutCashSessionId()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Seller);
        var user = MockUser(companyId);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(employee.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var handler = new CreatePayrollAdvanceHandler(
            user.Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            employeeRepository.Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreatePayrollAdvanceCommand(employee.Id.Value, 15000m, DateTime.UtcNow, null, (int)PayrollPaymentMethod.Cash, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CancelHandler_ShouldFail_WhenAdvanceNotPending()
    {
        var companyId = CompanyId.New();
        var advance = PayrollAdvance.Create(companyId, EmployeeId.New(), 10000m, DateTime.UtcNow, null, eiti.Domain.Users.UserId.New());
        advance.Cancel();
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollAdvanceRepository>();
        repository
            .Setup(r => r.GetByIdAsync(advance.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var handler = new CancelPayrollAdvanceHandler(user.Object, repository.Object, new Mock<ICashSessionRepository>().Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelPayrollAdvanceCommand(advance.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ListHandler_ShouldReturnMappedItems()
    {
        var companyId = CompanyId.New();
        var advance = PayrollAdvance.Create(companyId, EmployeeId.New(), 10000m, DateTime.UtcNow, null, eiti.Domain.Users.UserId.New());
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollAdvanceRepository>();
        repository
            .Setup(r => r.ListByCompanyAsync(companyId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdvance> { advance });

        var handler = new ListPayrollAdvancesHandler(user.Object, repository.Object);

        var result = await handler.Handle(new ListPayrollAdvancesQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(x => x.Amount == 10000m);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter PayrollAdvanceHandlersTests`
Expected: compile error.

- [ ] **Step 4: Implement validators, errors, and handlers**

```csharp
using FluentValidation;
using eiti.Domain.Payroll;

namespace eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;

public sealed class CreatePayrollAdvanceValidator : AbstractValidator<CreatePayrollAdvanceCommand>
{
    public CreatePayrollAdvanceValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaymentMethod).Must(value => Enum.IsDefined(typeof(PayrollPaymentMethod), value));
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.CashSessionId)
            .NotNull()
            .WithMessage("A cash session is required when paying in cash.")
            .When(x => x.PaymentMethod == (int)PayrollPaymentMethod.Cash);
    }
}
```

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;

public static class CreatePayrollAdvanceErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Advances.Create.Unauthorized",
        "Authentication is required.");

    public static readonly Error EmployeeNotFound = Error.NotFound(
        "Payroll.Advances.Create.EmployeeNotFound",
        "The requested employee was not found.");

    public static readonly Error CashSessionNotFound = Error.NotFound(
        "Payroll.Advances.Create.CashSessionNotFound",
        "The requested cash session was not found.");

    public static readonly Error CashSessionRequired = Error.Validation(
        "Payroll.Advances.Create.CashSessionRequired",
        "A cash session is required when paying in cash.");
}
```

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Advances;
using eiti.Domain.Cash;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;

public sealed class CreatePayrollAdvanceHandler : IRequestHandler<CreatePayrollAdvanceCommand, Result<PayrollAdvanceResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollAdvanceRepository _advanceRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePayrollAdvanceHandler(
        ICurrentUserService currentUserService,
        IPayrollAdvanceRepository advanceRepository,
        IEmployeeRepository employeeRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _advanceRepository = advanceRepository;
        _employeeRepository = employeeRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollAdvanceResponse>> Handle(CreatePayrollAdvanceCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<PayrollAdvanceResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var employee = await _employeeRepository.GetByIdAsync(new EmployeeId(request.EmployeeId), companyId, cancellationToken);
        if (employee is null)
            return Result<PayrollAdvanceResponse>.Failure(CreatePayrollAdvanceErrors.EmployeeNotFound);

        var method = (PayrollPaymentMethod)request.PaymentMethod;

        if (method == PayrollPaymentMethod.Cash && request.CashSessionId is null)
            return Result<PayrollAdvanceResponse>.Failure(CreatePayrollAdvanceErrors.CashSessionRequired);

        CashSession? session = null;
        if (method == PayrollPaymentMethod.Cash)
        {
            session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(request.CashSessionId!.Value), companyId, cancellationToken);
            if (session is null)
                return Result<PayrollAdvanceResponse>.Failure(CreatePayrollAdvanceErrors.CashSessionNotFound);

            var accessCheck = await CashDrawerAccessPolicy.EnsureCanAccessDrawerAsync(
                _currentUserService, _cashDrawerRepository, session.CashDrawerId, cancellationToken);
            if (accessCheck.IsFailure)
                return Result<PayrollAdvanceResponse>.Failure(accessCheck.Error!);
        }

        var advance = PayrollAdvance.Create(companyId, employee.Id, request.Amount, request.Date, request.Notes, userId);

        if (method == PayrollPaymentMethod.Cash)
        {
            try
            {
                session!.RegisterPayrollAdvanceExpense(request.Amount, advance.Id.Value, userId);
            }
            catch (InvalidOperationException ex)
            {
                return Result<PayrollAdvanceResponse>.Failure(Error.Conflict("Payroll.Advances.Create.CashConflict", ex.Message));
            }
        }

        await _advanceRepository.AddAsync(advance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollAdvanceResponse>.Success(
            new PayrollAdvanceResponse(advance.Id.Value, advance.EmployeeId.Value, advance.Amount, advance.Date, advance.Notes, (int)advance.Status, advance.AppliedToLiquidationId?.Value));
    }
}
```

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Advances.Commands.CancelPayrollAdvance;

public static class CancelPayrollAdvanceErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Advances.Cancel.NotFound",
        "The requested advance was not found.");
}
```

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Advances;
using eiti.Domain.Cash;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Commands.CancelPayrollAdvance;

public sealed class CancelPayrollAdvanceHandler : IRequestHandler<CancelPayrollAdvanceCommand, Result<PayrollAdvanceResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollAdvanceRepository _advanceRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPayrollAdvanceHandler(
        ICurrentUserService currentUserService,
        IPayrollAdvanceRepository advanceRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _advanceRepository = advanceRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollAdvanceResponse>> Handle(CancelPayrollAdvanceCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<PayrollAdvanceResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var advance = await _advanceRepository.GetByIdAsync(new PayrollAdvanceId(request.Id), companyId, cancellationToken);
        if (advance is null)
            return Result<PayrollAdvanceResponse>.Failure(CancelPayrollAdvanceErrors.NotFound);

        try
        {
            advance.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return Result<PayrollAdvanceResponse>.Failure(Error.Conflict("Payroll.Advances.Cancel.InvalidOperation", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollAdvanceResponse>.Success(
            new PayrollAdvanceResponse(advance.Id.Value, advance.EmployeeId.Value, advance.Amount, advance.Date, advance.Notes, (int)advance.Status, advance.AppliedToLiquidationId?.Value));
    }
}
```

Note: cancelling a *cash* advance's `CashMovement` reversal is intentionally **not implemented in this task** — reverting cash on advance cancellation requires resolving which `CashSession` the original expense hit, which is not stored on `PayrollAdvance` itself (only on the `CashMovement`). If this is needed, add a `CashSessionId: Guid?` property to `PayrollAdvance` in a follow-up task, mirroring `PayrollLiquidation.CashSessionId`, and call `RegisterPayrollAdvanceExpenseCancel` here. Flagged rather than guessed, since the spec didn't cover this specific edge case explicitly — confirm with the user before extending scope.

```csharp
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Advances;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Queries.ListPayrollAdvances;

public sealed class ListPayrollAdvancesHandler : IRequestHandler<ListPayrollAdvancesQuery, Result<IReadOnlyList<PayrollAdvanceResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollAdvanceRepository _repository;

    public ListPayrollAdvancesHandler(ICurrentUserService currentUserService, IPayrollAdvanceRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<PayrollAdvanceResponse>>> Handle(ListPayrollAdvancesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<PayrollAdvanceResponse>>.Failure(authCheck.Error);

        var employeeId = request.EmployeeId.HasValue ? new EmployeeId(request.EmployeeId.Value) : null;
        var status = request.Status.HasValue ? (PayrollAdvanceStatus)request.Status.Value : (PayrollAdvanceStatus?)null;

        var advances = await _repository.ListByCompanyAsync(_currentUserService.CompanyId!, employeeId, status, cancellationToken);

        IReadOnlyList<PayrollAdvanceResponse> items = advances
            .Select(a => new PayrollAdvanceResponse(a.Id.Value, a.EmployeeId.Value, a.Amount, a.Date, a.Notes, (int)a.Status, a.AppliedToLiquidationId?.Value))
            .ToList();

        return Result<IReadOnlyList<PayrollAdvanceResponse>>.Success(items);
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter PayrollAdvanceHandlersTests`
Expected: 4 passed.

- [ ] **Step 6: Commit**

```bash
git add eiti.Application/Features/Payroll/Advances/ eiti.Tests/PayrollAdvanceHandlersTests.cs
git commit -m "feat(payroll): adelantos de sueldo (crear, cancelar, listar)"
```

---

## Task 12: Generate payroll period (batch)

**Files:**
- Create: `eiti.Application/Features/Payroll/Liquidations/GeneratePayrollPeriod/GeneratePayrollPeriodCommand.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/GeneratePayrollPeriod/GeneratePayrollPeriodValidator.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/GeneratePayrollPeriod/GeneratePayrollPeriodResponse.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/GeneratePayrollPeriod/GeneratePayrollPeriodHandler.cs`
- Test: `eiti.Tests/GeneratePayrollPeriodHandlerTests.cs`

**Interfaces:**
- Consumes: `IEmployeeRepository.ListByCompanyAsync` (existing), `IPayrollDeductionConceptRepository.ListByCompanyAsync` (Task 7), `IPayrollAdvanceRepository.ListPendingByEmployeeAsync` (Task 7), `IPayrollLiquidationRepository.ExistsForPeriodAsync` / `AddAsync` (Task 7), `PayrollAdvance.Apply` (Task 3), `PayrollLiquidation.Create`, `PayrollLiquidationDeductionLine.Create`, `PayrollLiquidationAdvanceLine.Create` (Task 4).
- Produces: `GeneratePayrollPeriodResponse(int GeneratedCount, IReadOnlyList<PayrollLiquidationSummary> Generated, IReadOnlyList<GeneratePayrollPeriodSkippedItem> Skipped)`.

- [ ] **Step 1: Response + command + validator**

```csharp
namespace eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;

public sealed record PayrollLiquidationSummary(Guid Id, Guid EmployeeId, string EmployeeName, decimal NetAmount);

public sealed record GeneratePayrollPeriodSkippedItem(Guid EmployeeId, string EmployeeName, string Reason);

public sealed record GeneratePayrollPeriodResponse(
    int GeneratedCount,
    IReadOnlyList<PayrollLiquidationSummary> Generated,
    IReadOnlyList<GeneratePayrollPeriodSkippedItem> Skipped);
```

```csharp
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;

public sealed record GeneratePayrollPeriodCommand(int Periodicity, string PeriodLabel, DateTime PeriodStart, DateTime PeriodEnd)
    : IRequest<Result<GeneratePayrollPeriodResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollLiquidationsGenerate];
}
```

```csharp
using eiti.Domain.Employees;
using FluentValidation;

namespace eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;

public sealed class GeneratePayrollPeriodValidator : AbstractValidator<GeneratePayrollPeriodCommand>
{
    public GeneratePayrollPeriodValidator()
    {
        RuleFor(x => x.Periodicity).Must(value => Enum.IsDefined(typeof(PayrollPeriodicity), value));
        RuleFor(x => x.PeriodLabel).NotEmpty().MaximumLength(20);
        RuleFor(x => x.PeriodEnd).GreaterThan(x => x.PeriodStart);
    }
}
```

- [ ] **Step 2: Write the failing handler tests**

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class GeneratePayrollPeriodHandlerTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        user.SetupGet(u => u.UserId).Returns(eiti.Domain.Users.UserId.New());
        return user;
    }

    [Fact]
    public async Task Handle_ShouldGenerateLiquidation_ForEligibleEmployee_WithDeductionsAndAdvancesApplied()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Seller);
        employee.SetPayrollConfig(500000m, PayrollPeriodicity.Monthly);

        var user = MockUser(companyId);
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { employee });

        var concept = PayrollDeductionConcept.Create(companyId, "Jubilacion", 11m);
        var deductionRepository = new Mock<IPayrollDeductionConceptRepository>();
        deductionRepository
            .Setup(r => r.ListByCompanyAsync(companyId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollDeductionConcept> { concept });

        var advance = PayrollAdvance.Create(companyId, employee.Id, 20000m, DateTime.UtcNow, null, eiti.Domain.Users.UserId.New());
        var advanceRepository = new Mock<IPayrollAdvanceRepository>();
        advanceRepository
            .Setup(r => r.ListPendingByEmployeeAsync(companyId, employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdvance> { advance });

        var liquidationRepository = new Mock<IPayrollLiquidationRepository>();
        liquidationRepository
            .Setup(r => r.ExistsForPeriodAsync(companyId, employee.Id, "2026-07", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        PayrollLiquidation? persisted = null;
        liquidationRepository
            .Setup(r => r.AddAsync(It.IsAny<PayrollLiquidation>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollLiquidation, CancellationToken>((l, _) => persisted = l)
            .Returns(Task.CompletedTask);

        var handler = new GeneratePayrollPeriodHandler(
            user.Object,
            employeeRepository.Object,
            deductionRepository.Object,
            advanceRepository.Object,
            liquidationRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new GeneratePayrollPeriodCommand((int)PayrollPeriodicity.Monthly, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.GeneratedCount.Should().Be(1);
        persisted.Should().NotBeNull();
        persisted!.NetAmount.Should().Be(425000m); // 500000 - 55000 (11%) - 20000 (adelanto)
        advance.Status.Should().Be(PayrollAdvanceStatus.Applied);
    }

    [Fact]
    public async Task Handle_ShouldSkip_WhenEmployeeHasNoBaseSalary()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Seller);
        var user = MockUser(companyId);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { employee });

        var handler = new GeneratePayrollPeriodHandler(
            user.Object,
            employeeRepository.Object,
            new Mock<IPayrollDeductionConceptRepository>().Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            new Mock<IPayrollLiquidationRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new GeneratePayrollPeriodCommand((int)PayrollPeriodicity.Monthly, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.GeneratedCount.Should().Be(0);
        result.Value.Skipped.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldSkip_WhenLiquidationAlreadyExistsForPeriod()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Seller);
        employee.SetPayrollConfig(500000m, PayrollPeriodicity.Monthly);
        var user = MockUser(companyId);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { employee });

        var liquidationRepository = new Mock<IPayrollLiquidationRepository>();
        liquidationRepository
            .Setup(r => r.ExistsForPeriodAsync(companyId, employee.Id, "2026-07", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new GeneratePayrollPeriodHandler(
            user.Object,
            employeeRepository.Object,
            new Mock<IPayrollDeductionConceptRepository>().Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            liquidationRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new GeneratePayrollPeriodCommand((int)PayrollPeriodicity.Monthly, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)),
            CancellationToken.None);

        result.Value.GeneratedCount.Should().Be(0);
        result.Value.Skipped.Single().Reason.Should().Contain("período");
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter GeneratePayrollPeriodHandlerTests`
Expected: compile error.

- [ ] **Step 4: Implement the handler**

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;

public sealed class GeneratePayrollPeriodHandler : IRequestHandler<GeneratePayrollPeriodCommand, Result<GeneratePayrollPeriodResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IPayrollDeductionConceptRepository _deductionConceptRepository;
    private readonly IPayrollAdvanceRepository _advanceRepository;
    private readonly IPayrollLiquidationRepository _liquidationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public GeneratePayrollPeriodHandler(
        ICurrentUserService currentUserService,
        IEmployeeRepository employeeRepository,
        IPayrollDeductionConceptRepository deductionConceptRepository,
        IPayrollAdvanceRepository advanceRepository,
        IPayrollLiquidationRepository liquidationRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _deductionConceptRepository = deductionConceptRepository;
        _advanceRepository = advanceRepository;
        _liquidationRepository = liquidationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GeneratePayrollPeriodResponse>> Handle(GeneratePayrollPeriodCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GeneratePayrollPeriodResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var periodicity = (PayrollPeriodicity)request.Periodicity;

        var employees = await _employeeRepository.ListByCompanyAsync(companyId, cancellationToken);
        var activeConcepts = await _deductionConceptRepository.ListByCompanyAsync(companyId, activeOnly: true, cancellationToken);

        var generated = new List<PayrollLiquidationSummary>();
        var skipped = new List<GeneratePayrollPeriodSkippedItem>();

        foreach (var employee in employees.Where(e => e.IsActive))
        {
            if (employee.BaseSalary is null || employee.PayrollPeriodicity != periodicity)
            {
                skipped.Add(new GeneratePayrollPeriodSkippedItem(employee.Id.Value, employee.FullName, "Sin sueldo base configurado para esta periodicidad."));
                continue;
            }

            if (await _liquidationRepository.ExistsForPeriodAsync(companyId, employee.Id, request.PeriodLabel, cancellationToken))
            {
                skipped.Add(new GeneratePayrollPeriodSkippedItem(employee.Id.Value, employee.FullName, $"Ya tiene una liquidación para el período {request.PeriodLabel}."));
                continue;
            }

            var deductionLines = activeConcepts
                .Select(concept => PayrollLiquidationDeductionLine.Create(
                    concept.Name,
                    concept.Percentage,
                    decimal.Round(employee.BaseSalary.Value * concept.Percentage / 100m, 2, MidpointRounding.AwayFromZero)))
                .ToList();

            var pendingAdvances = await _advanceRepository.ListPendingByEmployeeAsync(companyId, employee.Id, cancellationToken);
            var advanceLines = pendingAdvances
                .Select(advance => PayrollLiquidationAdvanceLine.Create(advance.Id.Value, advance.Amount))
                .ToList();

            var liquidation = PayrollLiquidation.Create(
                companyId,
                employee.Id,
                employee.BranchId,
                request.PeriodLabel,
                request.PeriodStart,
                request.PeriodEnd,
                employee.BaseSalary.Value,
                deductionLines,
                advanceLines);

            foreach (var advance in pendingAdvances)
            {
                advance.Apply(liquidation.Id);
            }

            await _liquidationRepository.AddAsync(liquidation, cancellationToken);
            generated.Add(new PayrollLiquidationSummary(liquidation.Id.Value, employee.Id.Value, employee.FullName, liquidation.NetAmount));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<GeneratePayrollPeriodResponse>.Success(
            new GeneratePayrollPeriodResponse(generated.Count, generated, skipped));
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter GeneratePayrollPeriodHandlerTests`
Expected: 3 passed.

- [ ] **Step 6: Commit**

```bash
git add eiti.Application/Features/Payroll/Liquidations/GeneratePayrollPeriod/ eiti.Tests/GeneratePayrollPeriodHandlerTests.cs
git commit -m "feat(payroll): generacion en lote de liquidaciones por periodo"
```

---

## Task 13: Pay liquidation

**Files:**
- Create: `eiti.Application/Features/Payroll/Liquidations/PayrollLiquidationResponse.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/PayLiquidation/PayLiquidationCommand.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/PayLiquidation/PayLiquidationValidator.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/PayLiquidation/PayLiquidationErrors.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/PayLiquidation/PayLiquidationHandler.cs`
- Test: `eiti.Tests/PayLiquidationHandlerTests.cs`

**Interfaces:**
- Consumes: `IPayrollLiquidationRepository.GetByIdAsync` (Task 7), `ICashDrawerRepository`/`ICashSessionRepository`/`CashDrawerAccessPolicy` (existing), `CashSession.RegisterPayrollExpense` (Task 5), `PayrollLiquidation.MarkAsPaid` (Task 4).
- Produces: `PayrollLiquidationResponse(Guid Id, Guid EmployeeId, string PeriodLabel, decimal GrossAmount, decimal NetAmount, int Status, int? PaymentMethod, DateTime? PaidAt, IReadOnlyList<PayrollLiquidationLineResponse> DeductionLines, IReadOnlyList<PayrollLiquidationLineResponse> AdvanceLines)` — shared response used by Pay (this task), Cancel (Task 14), and the queries (Task 15).

- [ ] **Step 1: Shared response + command + validator**

```csharp
namespace eiti.Application.Features.Payroll.Liquidations;

public sealed record PayrollLiquidationLineResponse(string Label, decimal Amount);

public sealed record PayrollLiquidationResponse(
    Guid Id,
    Guid EmployeeId,
    string PeriodLabel,
    decimal GrossAmount,
    decimal NetAmount,
    int Status,
    int? PaymentMethod,
    DateTime? PaidAt,
    IReadOnlyList<PayrollLiquidationLineResponse> DeductionLines,
    IReadOnlyList<PayrollLiquidationLineResponse> AdvanceLines);
```

```csharp
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Liquidations;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.PayLiquidation;

public sealed record PayLiquidationCommand(Guid LiquidationId, int PaymentMethod, Guid? CashSessionId)
    : IRequest<Result<PayrollLiquidationResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollLiquidationsPay];
}
```

```csharp
using eiti.Domain.Payroll;
using FluentValidation;

namespace eiti.Application.Features.Payroll.Liquidations.PayLiquidation;

public sealed class PayLiquidationValidator : AbstractValidator<PayLiquidationCommand>
{
    public PayLiquidationValidator()
    {
        RuleFor(x => x.LiquidationId).NotEmpty();
        RuleFor(x => x.PaymentMethod).Must(value => Enum.IsDefined(typeof(PayrollPaymentMethod), value));
        RuleFor(x => x.CashSessionId)
            .NotNull()
            .WithMessage("A cash session is required when paying in cash.")
            .When(x => x.PaymentMethod == (int)PayrollPaymentMethod.Cash);
    }
}
```

- [ ] **Step 2: Write the failing handler tests**

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Liquidations.PayLiquidation;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class PayLiquidationHandlerTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        user.SetupGet(u => u.UserId).Returns(eiti.Domain.Users.UserId.New());
        return user;
    }

    private static PayrollLiquidation CreateLiquidation(CompanyId companyId, decimal grossAmount = 500000m)
    {
        return PayrollLiquidation.Create(companyId, EmployeeId.New(), null, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), grossAmount, [], []);
    }

    [Fact]
    public async Task Handle_ShouldMarkAsPaid_WhenTransfer()
    {
        var companyId = CompanyId.New();
        var liquidation = CreateLiquidation(companyId);
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollLiquidationRepository>();
        repository
            .Setup(r => r.GetByIdAsync(liquidation.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(liquidation);

        var handler = new PayLiquidationHandler(
            user.Object,
            repository.Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new PayLiquidationCommand(liquidation.Id.Value, (int)PayrollPaymentMethod.Transfer, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be((int)PayrollLiquidationStatus.Paid);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLiquidationNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollLiquidationRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollLiquidationId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollLiquidation?)null);

        var handler = new PayLiquidationHandler(
            user.Object,
            repository.Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new PayLiquidationCommand(Guid.NewGuid(), (int)PayrollPaymentMethod.Transfer, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenCashSessionNotFound()
    {
        var companyId = CompanyId.New();
        var liquidation = CreateLiquidation(companyId);
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollLiquidationRepository>();
        repository
            .Setup(r => r.GetByIdAsync(liquidation.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(liquidation);

        var cashSessionRepository = new Mock<ICashSessionRepository>();
        cashSessionRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<eiti.Domain.Cash.CashSessionId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((eiti.Domain.Cash.CashSession?)null);

        var handler = new PayLiquidationHandler(
            user.Object,
            repository.Object,
            new Mock<ICashDrawerRepository>().Object,
            cashSessionRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new PayLiquidationCommand(liquidation.Id.Value, (int)PayrollPaymentMethod.Cash, Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter PayLiquidationHandlerTests`
Expected: compile error.

- [ ] **Step 4: Implement errors + handler**

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Liquidations.PayLiquidation;

public static class PayLiquidationErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Liquidations.Pay.NotFound",
        "The requested liquidation was not found.");

    public static readonly Error CashSessionNotFound = Error.NotFound(
        "Payroll.Liquidations.Pay.CashSessionNotFound",
        "The requested cash session was not found.");

    public static readonly Error CashSessionRequired = Error.Validation(
        "Payroll.Liquidations.Pay.CashSessionRequired",
        "A cash session is required when paying in cash.");
}
```

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Liquidations;
using eiti.Domain.Cash;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.PayLiquidation;

public sealed class PayLiquidationHandler : IRequestHandler<PayLiquidationCommand, Result<PayrollLiquidationResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollLiquidationRepository _liquidationRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PayLiquidationHandler(
        ICurrentUserService currentUserService,
        IPayrollLiquidationRepository liquidationRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _liquidationRepository = liquidationRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollLiquidationResponse>> Handle(PayLiquidationCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<PayrollLiquidationResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var liquidation = await _liquidationRepository.GetByIdAsync(new PayrollLiquidationId(request.LiquidationId), companyId, cancellationToken);
        if (liquidation is null)
            return Result<PayrollLiquidationResponse>.Failure(PayLiquidationErrors.NotFound);

        var method = (PayrollPaymentMethod)request.PaymentMethod;

        if (method == PayrollPaymentMethod.Cash && request.CashSessionId is null)
            return Result<PayrollLiquidationResponse>.Failure(PayLiquidationErrors.CashSessionRequired);

        CashSession? session = null;
        if (method == PayrollPaymentMethod.Cash)
        {
            session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(request.CashSessionId!.Value), companyId, cancellationToken);
            if (session is null)
                return Result<PayrollLiquidationResponse>.Failure(PayLiquidationErrors.CashSessionNotFound);

            var accessCheck = await CashDrawerAccessPolicy.EnsureCanAccessDrawerAsync(
                _currentUserService, _cashDrawerRepository, session.CashDrawerId, cancellationToken);
            if (accessCheck.IsFailure)
                return Result<PayrollLiquidationResponse>.Failure(accessCheck.Error!);
        }

        try
        {
            liquidation.MarkAsPaid(method, session?.Id.Value);

            if (method == PayrollPaymentMethod.Cash)
            {
                session!.RegisterPayrollExpense(liquidation.NetAmount, liquidation.Id.Value, userId);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<PayrollLiquidationResponse>.Failure(Error.Conflict("Payroll.Liquidations.Pay.InvalidOperation", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollLiquidationResponse>.Success(PayrollLiquidationMapper.Map(liquidation));
    }
}
```

- [ ] **Step 5: Create the shared mapper**

Both this task and Tasks 14-15 build the same response shape — extract it once:

```csharp
namespace eiti.Application.Features.Payroll.Liquidations;

internal static class PayrollLiquidationMapper
{
    public static PayrollLiquidationResponse Map(eiti.Domain.Payroll.PayrollLiquidation liquidation)
    {
        return new PayrollLiquidationResponse(
            liquidation.Id.Value,
            liquidation.EmployeeId.Value,
            liquidation.PeriodLabel,
            liquidation.GrossAmount,
            liquidation.NetAmount,
            (int)liquidation.Status,
            (int?)liquidation.PaymentMethod,
            liquidation.PaidAt,
            liquidation.DeductionLines.Select(l => new PayrollLiquidationLineResponse(l.ConceptName, l.Amount)).ToList(),
            liquidation.AdvanceLines.Select(l => new PayrollLiquidationLineResponse("Adelanto", l.Amount)).ToList());
    }
}
```

Save this to `eiti.Application/Features/Payroll/Liquidations/PayrollLiquidationMapper.cs`, and replace the inline object-construction in `PayLiquidationHandler.Handle`'s final `return` with the call to `PayrollLiquidationMapper.Map(liquidation)` shown above (already reflected in Step 4's code).

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter PayLiquidationHandlerTests`
Expected: 3 passed.

- [ ] **Step 7: Commit**

```bash
git add eiti.Application/Features/Payroll/Liquidations/PayrollLiquidationResponse.cs eiti.Application/Features/Payroll/Liquidations/PayrollLiquidationMapper.cs eiti.Application/Features/Payroll/Liquidations/PayLiquidation/ eiti.Tests/PayLiquidationHandlerTests.cs
git commit -m "feat(payroll): pago de liquidacion (efectivo/transferencia/otro)"
```

---

## Task 14: Cancel liquidation

**Files:**
- Create: `eiti.Application/Features/Payroll/Liquidations/CancelLiquidation/CancelLiquidationCommand.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/CancelLiquidation/CancelLiquidationErrors.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/CancelLiquidation/CancelLiquidationHandler.cs`
- Test: `eiti.Tests/CancelLiquidationHandlerTests.cs`

**Interfaces:**
- Consumes: `IPayrollLiquidationRepository.GetByIdAsync`, `IPayrollAdvanceRepository.GetByIdAsync`, `ICashSessionRepository.GetByIdAsync`, `PayrollAdvance.Revert` (Task 3), `CashSession.RegisterPayrollExpenseCancel` (Task 5), `PayrollLiquidation.Cancel` (Task 4).
- Produces: `PayrollLiquidationResponse` (Task 13's shared type).

**Design note (edge case from the spec):** if the liquidation was paid in cash and its original `CashSession` is now closed, `RegisterPayrollExpenseCancel`'s internal `EnsureOpen()` throws `InvalidOperationException` — the handler catches that and returns `Error.Conflict`, matching the spec's "rechaza en vez de reabrir la caja" rule for free, no extra code needed.

- [ ] **Step 1: Command**

```csharp
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Liquidations;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.CancelLiquidation;

public sealed record CancelLiquidationCommand(Guid LiquidationId) : IRequest<Result<PayrollLiquidationResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollLiquidationsPay];
}
```

- [ ] **Step 2: Write the failing handler tests**

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Liquidations.CancelLiquidation;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CancelLiquidationHandlerTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        user.SetupGet(u => u.UserId).Returns(eiti.Domain.Users.UserId.New());
        return user;
    }

    [Fact]
    public async Task Handle_ShouldCancelLiquidation_AndRevertAppliedAdvances_WhenPaidByTransfer()
    {
        var companyId = CompanyId.New();
        var employeeId = EmployeeId.New();
        var advance = PayrollAdvance.Create(companyId, employeeId, 20000m, DateTime.UtcNow, null, eiti.Domain.Users.UserId.New());
        var advanceLine = PayrollLiquidationAdvanceLine.Create(advance.Id.Value, 20000m);
        var liquidation = PayrollLiquidation.Create(companyId, employeeId, null, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), 500000m, [], [advanceLine]);
        advance.Apply(liquidation.Id);
        liquidation.MarkAsPaid(PayrollPaymentMethod.Transfer, null);

        var user = MockUser(companyId);
        var liquidationRepository = new Mock<IPayrollLiquidationRepository>();
        liquidationRepository
            .Setup(r => r.GetByIdAsync(liquidation.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(liquidation);

        var advanceRepository = new Mock<IPayrollAdvanceRepository>();
        advanceRepository
            .Setup(r => r.GetByIdAsync(advance.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var handler = new CancelLiquidationHandler(
            user.Object,
            liquidationRepository.Object,
            advanceRepository.Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelLiquidationCommand(liquidation.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        liquidation.Status.Should().Be(PayrollLiquidationStatus.Cancelled);
        advance.Status.Should().Be(PayrollAdvanceStatus.Pending);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLiquidationNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);

        var liquidationRepository = new Mock<IPayrollLiquidationRepository>();
        liquidationRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollLiquidationId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollLiquidation?)null);

        var handler = new CancelLiquidationHandler(
            user.Object,
            liquidationRepository.Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelLiquidationCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter CancelLiquidationHandlerTests`
Expected: compile error.

- [ ] **Step 4: Implement errors + handler**

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Liquidations.CancelLiquidation;

public static class CancelLiquidationErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Liquidations.Cancel.NotFound",
        "The requested liquidation was not found.");
}
```

```csharp
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Liquidations;
using eiti.Domain.Cash;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.CancelLiquidation;

public sealed class CancelLiquidationHandler : IRequestHandler<CancelLiquidationCommand, Result<PayrollLiquidationResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollLiquidationRepository _liquidationRepository;
    private readonly IPayrollAdvanceRepository _advanceRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelLiquidationHandler(
        ICurrentUserService currentUserService,
        IPayrollLiquidationRepository liquidationRepository,
        IPayrollAdvanceRepository advanceRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _liquidationRepository = liquidationRepository;
        _advanceRepository = advanceRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollLiquidationResponse>> Handle(CancelLiquidationCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<PayrollLiquidationResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var liquidation = await _liquidationRepository.GetByIdAsync(new PayrollLiquidationId(request.LiquidationId), companyId, cancellationToken);
        if (liquidation is null)
            return Result<PayrollLiquidationResponse>.Failure(CancelLiquidationErrors.NotFound);

        try
        {
            if (liquidation.PaymentMethod == PayrollPaymentMethod.Cash && liquidation.CashSessionId.HasValue)
            {
                var session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(liquidation.CashSessionId.Value), companyId, cancellationToken);
                if (session is null)
                    return Result<PayrollLiquidationResponse>.Failure(CancelLiquidationErrors.NotFound);

                session.RegisterPayrollExpenseCancel(liquidation.NetAmount, liquidation.Id.Value, userId);
            }

            foreach (var advanceLine in liquidation.AdvanceLines)
            {
                var advance = await _advanceRepository.GetByIdAsync(new PayrollAdvanceId(advanceLine.PayrollAdvanceId), companyId, cancellationToken);
                advance?.Revert();
            }

            liquidation.Cancel();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<PayrollLiquidationResponse>.Failure(Error.Conflict("Payroll.Liquidations.Cancel.InvalidOperation", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollLiquidationResponse>.Success(PayrollLiquidationMapper.Map(liquidation));
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter CancelLiquidationHandlerTests`
Expected: 2 passed.

- [ ] **Step 6: Commit**

```bash
git add eiti.Application/Features/Payroll/Liquidations/CancelLiquidation/ eiti.Tests/CancelLiquidationHandlerTests.cs
git commit -m "feat(payroll): cancelacion de liquidacion (revierte adelantos y caja)"
```

---

## Task 15: List and GetById queries for liquidations

**Files:**
- Create: `eiti.Application/Features/Payroll/Liquidations/ListLiquidations/ListLiquidationsQuery.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/ListLiquidations/ListLiquidationsResponse.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/ListLiquidations/ListLiquidationsHandler.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/GetLiquidationById/GetLiquidationByIdQuery.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/GetLiquidationById/GetLiquidationByIdErrors.cs`
- Create: `eiti.Application/Features/Payroll/Liquidations/GetLiquidationById/GetLiquidationByIdHandler.cs`
- Test: `eiti.Tests/LiquidationQueriesTests.cs`

**Interfaces:**
- Consumes: `IPayrollLiquidationRepository.ListAsync` / `.CountAsync` / `.GetByIdAsync` (Task 7), `PayrollLiquidationMapper.Map` (Task 13).
- Produces: `ListLiquidationsResponse(IReadOnlyList<PayrollLiquidationResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages)` — same pagination shape as `ListAuditLogResponse`; `GetLiquidationByIdQuery` returns `Result<PayrollLiquidationResponse>` directly (detail for the receipt).

- [ ] **Step 1: Query records + response**

```csharp
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Liquidations;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.ListLiquidations;

public sealed record ListLiquidationsQuery(
    Guid? EmployeeId,
    string? PeriodLabel,
    int? Status,
    int Page,
    int PageSize) : IRequest<Result<ListLiquidationsResponse>>;
```

```csharp
using eiti.Application.Features.Payroll.Liquidations;

namespace eiti.Application.Features.Payroll.Liquidations.ListLiquidations;

public sealed record ListLiquidationsResponse(
    IReadOnlyList<PayrollLiquidationResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
```

```csharp
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Liquidations;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.GetLiquidationById;

public sealed record GetLiquidationByIdQuery(Guid LiquidationId) : IRequest<Result<PayrollLiquidationResponse>>;
```

- [ ] **Step 2: Write the failing tests**

```csharp
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Liquidations.GetLiquidationById;
using eiti.Application.Features.Payroll.Liquidations.ListLiquidations;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class LiquidationQueriesTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        return user;
    }

    [Fact]
    public async Task ListHandler_ShouldReturnPagedItems()
    {
        var companyId = CompanyId.New();
        var liquidation = PayrollLiquidation.Create(companyId, EmployeeId.New(), null, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), 500000m, [], []);
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollLiquidationRepository>();
        repository
            .Setup(r => r.ListAsync(companyId, null, null, null, 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollLiquidation> { liquidation });
        repository
            .Setup(r => r.CountAsync(companyId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new ListLiquidationsHandler(user.Object, repository.Object);

        var result = await handler.Handle(new ListLiquidationsQuery(null, null, null, 1, 25), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdHandler_ShouldFail_WhenNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollLiquidationRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollLiquidationId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollLiquidation?)null);

        var handler = new GetLiquidationByIdHandler(user.Object, repository.Object);

        var result = await handler.Handle(new GetLiquidationByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test eiti.Tests --filter LiquidationQueriesTests`
Expected: compile error.

- [ ] **Step 4: Implement handlers**

```csharp
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Liquidations;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.ListLiquidations;

public sealed class ListLiquidationsHandler : IRequestHandler<ListLiquidationsQuery, Result<ListLiquidationsResponse>>
{
    private const int MaxPageSize = 200;

    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollLiquidationRepository _repository;

    public ListLiquidationsHandler(ICurrentUserService currentUserService, IPayrollLiquidationRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<ListLiquidationsResponse>> Handle(ListLiquidationsQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<ListLiquidationsResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var employeeId = request.EmployeeId.HasValue ? new EmployeeId(request.EmployeeId.Value) : null;
        var status = request.Status.HasValue ? (PayrollLiquidationStatus)request.Status.Value : (PayrollLiquidationStatus?)null;
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 25 : Math.Min(request.PageSize, MaxPageSize);

        var totalCount = await _repository.CountAsync(companyId, employeeId, request.PeriodLabel, status, cancellationToken);
        var liquidations = await _repository.ListAsync(companyId, employeeId, request.PeriodLabel, status, page, pageSize, cancellationToken);

        var items = liquidations.Select(PayrollLiquidationMapper.Map).ToList();
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<ListLiquidationsResponse>.Success(new ListLiquidationsResponse(items, page, pageSize, totalCount, totalPages));
    }
}
```

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Liquidations.GetLiquidationById;

public static class GetLiquidationByIdErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Liquidations.GetById.NotFound",
        "The requested liquidation was not found.");
}
```

```csharp
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Liquidations;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.GetLiquidationById;

public sealed class GetLiquidationByIdHandler : IRequestHandler<GetLiquidationByIdQuery, Result<PayrollLiquidationResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollLiquidationRepository _repository;

    public GetLiquidationByIdHandler(ICurrentUserService currentUserService, IPayrollLiquidationRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<PayrollLiquidationResponse>> Handle(GetLiquidationByIdQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<PayrollLiquidationResponse>.Failure(authCheck.Error);

        var liquidation = await _repository.GetByIdAsync(new PayrollLiquidationId(request.LiquidationId), _currentUserService.CompanyId!, cancellationToken);
        if (liquidation is null)
            return Result<PayrollLiquidationResponse>.Failure(GetLiquidationByIdErrors.NotFound);

        return Result<PayrollLiquidationResponse>.Success(PayrollLiquidationMapper.Map(liquidation));
    }
}
```

Also change `PayrollLiquidationMapper.Map` from `internal static` to `public static` (Step 5 of Task 13) — it's now consumed from two additional namespaces (`ListLiquidations`, `GetLiquidationById`) that, despite being nested under `Features.Payroll.Liquidations`, are separate C# namespaces; `internal` still works within the same assembly, so this is optional-but-clearer. Leave `internal` if the build passes; only widen to `public` if Step 6 below reports `CS0122`.

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test eiti.Tests --filter LiquidationQueriesTests`
Expected: 2 passed.

- [ ] **Step 6: Commit**

```bash
git add eiti.Application/Features/Payroll/Liquidations/ListLiquidations/ eiti.Application/Features/Payroll/Liquidations/GetLiquidationById/ eiti.Tests/LiquidationQueriesTests.cs
git commit -m "feat(payroll): queries de listado y detalle de liquidaciones"
```

---

## Task 16: API controllers

**Files:**
- Create: `eiti.Api/Controllers/PayrollDeductionConceptsController.cs`
- Create: `eiti.Api/Controllers/PayrollAdvancesController.cs`
- Create: `eiti.Api/Controllers/PayrollLiquidationsController.cs`
- Modify: `eiti.Api/Controllers/EmployeesController.cs`

**Interfaces:**
- Consumes: every command/query from Tasks 9-15, `ISender` (MediatR), `result.ToActionResult()` (existing `eiti.Api.Extensions.ResultExtensions`).
- Produces: HTTP endpoints under `api/payroll-deduction-concepts`, `api/payroll-advances`, `api/payroll-liquidations`, and `PUT api/employees/{id}/payroll-config`.

This task has no unit test — controllers are thin pass-throughs already covered by the handler tests in Tasks 9-15. Verification is a full solution build (Step 2) plus a manual smoke check (Step 3).

- [ ] **Step 1: Implement the three new controllers**

```csharp
using eiti.Api.Extensions;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.SetDeductionConceptActive;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.UpdateDeductionConcept;
using eiti.Application.Features.Payroll.DeductionConcepts.Queries.ListDeductionConcepts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/payroll-deduction-concepts")]
[Authorize]
public sealed class PayrollDeductionConceptsController : ControllerBase
{
    private readonly ISender _sender;

    public PayrollDeductionConceptsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool activeOnly, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListDeductionConceptsQuery(activeOnly), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeductionConceptRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateDeductionConceptCommand(request.Name, request.Percentage), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDeductionConceptRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateDeductionConceptCommand(id, request.Name, request.Percentage), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetDeductionConceptActiveRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetDeductionConceptActiveCommand(id, request.IsActive), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record CreateDeductionConceptRequest(string Name, decimal Percentage);
public sealed record UpdateDeductionConceptRequest(string Name, decimal Percentage);
public sealed record SetDeductionConceptActiveRequest(bool IsActive);
```

```csharp
using eiti.Api.Extensions;
using eiti.Application.Features.Payroll.Advances.Commands.CancelPayrollAdvance;
using eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;
using eiti.Application.Features.Payroll.Advances.Queries.ListPayrollAdvances;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/payroll-advances")]
[Authorize]
public sealed class PayrollAdvancesController : ControllerBase
{
    private readonly ISender _sender;

    public PayrollAdvancesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? employeeId, [FromQuery] int? status, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListPayrollAdvancesQuery(employeeId, status), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePayrollAdvanceRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreatePayrollAdvanceCommand(request.EmployeeId, request.Amount, request.Date, request.Notes, request.PaymentMethod, request.CashSessionId),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelPayrollAdvanceCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record CreatePayrollAdvanceRequest(Guid EmployeeId, decimal Amount, DateTime Date, string? Notes, int PaymentMethod, Guid? CashSessionId);
```

```csharp
using eiti.Api.Extensions;
using eiti.Application.Features.Payroll.Liquidations.CancelLiquidation;
using eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;
using eiti.Application.Features.Payroll.Liquidations.GetLiquidationById;
using eiti.Application.Features.Payroll.Liquidations.ListLiquidations;
using eiti.Application.Features.Payroll.Liquidations.PayLiquidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/payroll-liquidations")]
[Authorize]
public sealed class PayrollLiquidationsController : ControllerBase
{
    private readonly ISender _sender;

    public PayrollLiquidationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? employeeId,
        [FromQuery] string? periodLabel,
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ListLiquidationsQuery(employeeId, periodLabel, status, page, pageSize), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetLiquidationByIdQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GeneratePayrollPeriodRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GeneratePayrollPeriodCommand(request.Periodicity, request.PeriodLabel, request.PeriodStart, request.PeriodEnd),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid id, [FromBody] PayLiquidationRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new PayLiquidationCommand(id, request.PaymentMethod, request.CashSessionId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelLiquidationCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record GeneratePayrollPeriodRequest(int Periodicity, string PeriodLabel, DateTime PeriodStart, DateTime PeriodEnd);
public sealed record PayLiquidationRequest(int PaymentMethod, Guid? CashSessionId);
```

- [ ] **Step 2: Wire `SetEmployeePayrollConfig` into `EmployeesController`**

Read `eiti.Api/Controllers/EmployeesController.cs` first to see its existing action list and add a new action following the same style (likely `[HttpPut("{id:guid}")]` already exists for the base `UpdateEmployeeCommand` — add a **new, distinct** route so it doesn't collide):

```csharp
    [HttpPut("{id:guid}/payroll-config")]
    public async Task<IActionResult> SetPayrollConfig(Guid id, [FromBody] SetEmployeePayrollConfigRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetEmployeePayrollConfigCommand(id, request.BaseSalary, request.PayrollPeriodicity), cancellationToken);
        return result.ToActionResult();
    }
```

Add the `using eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;` to the top of the file, and this record next to the file's other `public sealed record ...Request` declarations:

```csharp
public sealed record SetEmployeePayrollConfigRequest(decimal? BaseSalary, int? PayrollPeriodicity);
```

- [ ] **Step 3: Build the full solution and smoke-test**

Run: `dotnet build eiti.Api/eiti.Api.csproj`
Expected: 0 errors. (If the API process is running locally, stop it first — locked DLLs, per `CLAUDE.md`.)

Run the API locally and hit the new endpoints once manually (adjust the base URL/port to whatever `launchSettings.json` uses):

```bash
curl -X POST http://localhost:5000/api/payroll-deduction-concepts \
  -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
  -d '{"name":"Jubilacion","percentage":11}'
```

Expected: `200 OK` with a `DeductionConceptResponse` JSON body (or `401`/`403` if the test token lacks `payroll.manage` — confirms the permission gate is wired, which is also a valid outcome to verify).

- [ ] **Step 4: Run the full test suite one last time**

Run: `dotnet test eiti.Tests`
Expected: all tests pass, including every `Payroll*`/`*Payroll*` test from Tasks 1-15 plus the pre-existing suite (no regressions).

- [ ] **Step 5: Commit**

```bash
git add eiti.Api/Controllers/PayrollDeductionConceptsController.cs eiti.Api/Controllers/PayrollAdvancesController.cs eiti.Api/Controllers/PayrollLiquidationsController.cs eiti.Api/Controllers/EmployeesController.cs
git commit -m "feat(payroll): controllers de API para conceptos, adelantos y liquidaciones"
```

---

## End of backend plan

At this point the payroll module is fully usable via API (Postman/curl) and covered by unit tests for every domain invariant and handler branch. Next step: a separate frontend plan (Angular) once these endpoints are confirmed working end-to-end — see `docs/superpowers/specs/2026-07-09-payroll-design.md`'s Frontend and Recibo PDF sections for its scope.
