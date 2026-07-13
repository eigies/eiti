# Bonificaciones de Sueldo (Payroll Bonuses) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add per-employee salary bonuses (presentismo, bonificación por venta, and any future custom concept) that add to the net amount of a payroll liquidation — either as a fixed amount or as a percentage of the employee's base salary.

**Architecture:** Two new aggregates that mirror existing payroll patterns exactly: `PayrollBonusConcept` (a small named catalog, like `PayrollDeductionConcept` but without a fixed percentage) and `PayrollBonus` (a per-employee pending assignment, like `PayrollAdvance`, but it *adds* to the liquidation instead of subtracting). `GeneratePayrollPeriodHandler` sweeps an employee's pending bonuses into their liquidation the same way it already sweeps pending advances. `PayrollLiquidation.NetAmount` gains a `+ bonusLines` term.

**Tech Stack:** .NET 10, EF Core (Npgsql), MediatR, FluentValidation, xUnit + FluentAssertions + Moq (existing test stack).

## Global Constraints

- Backend only — this plan does not touch the Angular frontend. A separate frontend plan will be written afterward (same split used for the original payroll module: this repo builds backend via subagent-driven-development, the frontend plan is hooked up separately).
- Reuse `PermissionCodes.PayrollManage` for the bonus concept catalog CRUD (same level as deduction concepts) and `PermissionCodes.PayrollManage` for creating/cancelling bonuses too — per the approved spec, no new permission codes are introduced.
- Percentage bonuses are computed on `Employee.BaseSalary` only — never on sales data. Do not add any dependency from `eiti.Application/Features/Payroll` to `Features/Sales`.
- `decimal(18,2)` for all money columns, `decimal(5,2)` for percentage-typed value columns — same convention as `PayrollAdvance.Amount` / `PayrollDeductionConcept.Percentage`.
- Every handler starts with `EnsureAuthenticated()` (or `EnsureAuthenticatedWithContext()` when `UserId` is needed) followed by an explicit `CompanyId is null` guard — `EnsureAuthenticated()` alone does not guarantee `CompanyId`. Copy this pattern from `CreateDeductionConceptHandler` / `CreatePayrollAdvanceHandler` exactly; do not use unguarded `CompanyId!`.
- All new EF configurations use `HasConversion(id => id.Value, value => new XId(value))` for typed IDs — never raw `Guid` columns for aggregate keys (child line entities like `PayrollLiquidationBonusLine.PayrollBonusId` are the one exception, matching `PayrollLiquidationAdvanceLine.PayrollAdvanceId`, which is a plain `Guid`).
- Build every project WITH dependencies (`dotnet build eiti.Application/eiti.Application.csproj`, never `--no-dependencies`) before declaring a task done — stale cached DLLs produce false errors.

---

### Task 1: Domain — `PayrollBonusConcept` aggregate

**Files:**
- Create: `eiti.Domain/Payroll/PayrollBonusConceptId.cs`
- Create: `eiti.Domain/Payroll/PayrollBonusConcept.cs`
- Test: `eiti.Tests/PayrollBonusConceptTests.cs`

**Interfaces:**
- Produces: `PayrollBonusConceptId(Guid Value)` with `.New()`; `PayrollBonusConcept.Create(CompanyId, string name)`, `.Update(string name)`, `.Activate()`, `.Deactivate()`, properties `CompanyId`, `Name`, `IsActive`, `CreatedAt`.

- [ ] **Step 1: Write the failing tests**

```csharp
// eiti.Tests/PayrollBonusConceptTests.cs
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using FluentAssertions;

namespace eiti.Tests;

public sealed class PayrollBonusConceptTests
{
    [Fact]
    public void Create_ShouldStartActive()
    {
        var concept = PayrollBonusConcept.Create(CompanyId.New(), "Presentismo");

        concept.Name.Should().Be("Presentismo");
        concept.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNameIsEmpty()
    {
        var act = () => PayrollBonusConcept.Create(CompanyId.New(), "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNameExceeds150Characters()
    {
        var act = () => PayrollBonusConcept.Create(CompanyId.New(), new string('a', 151));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_ShouldChangeName()
    {
        var concept = PayrollBonusConcept.Create(CompanyId.New(), "Presentismo");

        concept.Update("Bonificacion por venta");

        concept.Name.Should().Be("Bonificacion por venta");
    }

    [Fact]
    public void Deactivate_ThenActivate_ShouldToggleIsActive()
    {
        var concept = PayrollBonusConcept.Create(CompanyId.New(), "Presentismo");

        concept.Deactivate();
        concept.IsActive.Should().BeFalse();

        concept.Activate();
        concept.IsActive.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter PayrollBonusConceptTests`
Expected: FAIL (compile error — `PayrollBonusConcept` does not exist)

- [ ] **Step 3: Implement `PayrollBonusConceptId`**

```csharp
// eiti.Domain/Payroll/PayrollBonusConceptId.cs
namespace eiti.Domain.Payroll;

public sealed record PayrollBonusConceptId(Guid Value)
{
    public static PayrollBonusConceptId New() => new(Guid.NewGuid());
}
```

- [ ] **Step 4: Implement `PayrollBonusConcept`**

```csharp
// eiti.Domain/Payroll/PayrollBonusConcept.cs
using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Payroll;

public sealed class PayrollBonusConcept : AggregateRoot<PayrollBonusConceptId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PayrollBonusConcept()
    {
    }

    private PayrollBonusConcept(PayrollBonusConceptId id, CompanyId companyId, string name)
        : base(id)
    {
        CompanyId = companyId;
        Name = NormalizeName(name);
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public static PayrollBonusConcept Create(CompanyId companyId, string name)
    {
        return new PayrollBonusConcept(PayrollBonusConceptId.New(), companyId, name);
    }

    public void Update(string name)
    {
        Name = NormalizeName(name);
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
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter PayrollBonusConceptTests`
Expected: PASS (5/5)

- [ ] **Step 6: Commit**

```bash
git add eiti.Domain/Payroll/PayrollBonusConceptId.cs eiti.Domain/Payroll/PayrollBonusConcept.cs eiti.Tests/PayrollBonusConceptTests.cs
git commit -m "feat(payroll): agregar aggregate PayrollBonusConcept"
```

---

### Task 2: Domain — `PayrollBonus` aggregate

**Files:**
- Create: `eiti.Domain/Payroll/PayrollBonusId.cs`
- Create: `eiti.Domain/Payroll/PayrollBonusAmountType.cs`
- Create: `eiti.Domain/Payroll/PayrollBonusStatus.cs`
- Create: `eiti.Domain/Payroll/PayrollBonus.cs`
- Test: `eiti.Tests/PayrollBonusTests.cs`

**Interfaces:**
- Consumes: `CompanyId`, `EmployeeId` (from `eiti.Domain.Companies` / `eiti.Domain.Employees`), `PayrollBonusConceptId` (Task 1), `PayrollLiquidationId` (existing).
- Produces: `PayrollBonusId.New()`; `PayrollBonus.Create(CompanyId, EmployeeId, PayrollBonusConceptId, PayrollBonusAmountType, decimal value, string? notes)`; instance methods `.Apply(PayrollLiquidationId)`, `.Cancel()`, `.RevertToPending()`, `.Resolve(decimal employeeBaseSalary)`; properties `CompanyId`, `EmployeeId`, `ConceptId`, `AmountType`, `Value`, `Notes`, `Status`, `PayrollLiquidationId`, `CreatedAt`.

- [ ] **Step 1: Write the failing tests**

```csharp
// eiti.Tests/PayrollBonusTests.cs
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;

namespace eiti.Tests;

public sealed class PayrollBonusTests
{
    private static PayrollBonus CreateFixedBonus(decimal value = 15000m) =>
        PayrollBonus.Create(CompanyId.New(), EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.FixedAmount, value, "Presentismo de julio");

    private static PayrollBonus CreatePercentageBonus(decimal value = 10m) =>
        PayrollBonus.Create(CompanyId.New(), EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.Percentage, value, null);

    [Fact]
    public void Create_ShouldStartAsPending()
    {
        var bonus = CreateFixedBonus();

        bonus.Status.Should().Be(PayrollBonusStatus.Pending);
        bonus.PayrollLiquidationId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldThrow_WhenValueIsZeroOrLess()
    {
        var act = () => PayrollBonus.Create(CompanyId.New(), EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.FixedAmount, 0m, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenPercentageExceeds100()
    {
        var act = () => PayrollBonus.Create(CompanyId.New(), EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.Percentage, 101m, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldAllowFixedAmountAboveOneHundred()
    {
        var bonus = CreateFixedBonus(500000m);

        bonus.Value.Should().Be(500000m);
    }

    [Fact]
    public void Resolve_ShouldReturnValue_WhenFixedAmount()
    {
        var bonus = CreateFixedBonus(15000m);

        bonus.Resolve(300000m).Should().Be(15000m);
    }

    [Fact]
    public void Resolve_ShouldReturnPercentageOfBaseSalary_WhenPercentage()
    {
        var bonus = CreatePercentageBonus(10m);

        bonus.Resolve(300000m).Should().Be(30000m);
    }

    [Fact]
    public void Apply_ShouldSetAppliedAndLiquidationId()
    {
        var bonus = CreateFixedBonus();
        var liquidationId = PayrollLiquidationId.New();

        bonus.Apply(liquidationId);

        bonus.Status.Should().Be(PayrollBonusStatus.Applied);
        bonus.PayrollLiquidationId.Should().Be(liquidationId);
    }

    [Fact]
    public void Apply_ShouldThrow_WhenNotPending()
    {
        var bonus = CreateFixedBonus();
        bonus.Apply(PayrollLiquidationId.New());

        var act = () => bonus.Apply(PayrollLiquidationId.New());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_ShouldSetCancelled_WhenPending()
    {
        var bonus = CreateFixedBonus();

        bonus.Cancel();

        bonus.Status.Should().Be(PayrollBonusStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldThrow_WhenNotPending()
    {
        var bonus = CreateFixedBonus();
        bonus.Apply(PayrollLiquidationId.New());

        var act = () => bonus.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RevertToPending_ShouldClearLiquidationId_WhenApplied()
    {
        var bonus = CreateFixedBonus();
        bonus.Apply(PayrollLiquidationId.New());

        bonus.RevertToPending();

        bonus.Status.Should().Be(PayrollBonusStatus.Pending);
        bonus.PayrollLiquidationId.Should().BeNull();
    }

    [Fact]
    public void RevertToPending_ShouldThrow_WhenNotApplied()
    {
        var bonus = CreateFixedBonus();

        var act = () => bonus.RevertToPending();

        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter PayrollBonusTests`
Expected: FAIL (compile error — types don't exist)

- [ ] **Step 3: Implement the enums**

```csharp
// eiti.Domain/Payroll/PayrollBonusAmountType.cs
namespace eiti.Domain.Payroll;

public enum PayrollBonusAmountType
{
    FixedAmount = 1,
    Percentage = 2
}
```

```csharp
// eiti.Domain/Payroll/PayrollBonusStatus.cs
namespace eiti.Domain.Payroll;

public enum PayrollBonusStatus
{
    Pending = 1,
    Applied = 2,
    Cancelled = 3
}
```

- [ ] **Step 4: Implement `PayrollBonusId`**

```csharp
// eiti.Domain/Payroll/PayrollBonusId.cs
namespace eiti.Domain.Payroll;

public sealed record PayrollBonusId(Guid Value)
{
    public static PayrollBonusId New() => new(Guid.NewGuid());
}
```

- [ ] **Step 5: Implement `PayrollBonus`**

```csharp
// eiti.Domain/Payroll/PayrollBonus.cs
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;

namespace eiti.Domain.Payroll;

public sealed class PayrollBonus : AggregateRoot<PayrollBonusId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public EmployeeId EmployeeId { get; private set; } = null!;
    public PayrollBonusConceptId ConceptId { get; private set; } = null!;
    public PayrollBonusAmountType AmountType { get; private set; }
    public decimal Value { get; private set; }
    public string? Notes { get; private set; }
    public PayrollBonusStatus Status { get; private set; }
    public PayrollLiquidationId? PayrollLiquidationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PayrollBonus()
    {
    }

    private PayrollBonus(
        PayrollBonusId id,
        CompanyId companyId,
        EmployeeId employeeId,
        PayrollBonusConceptId conceptId,
        PayrollBonusAmountType amountType,
        decimal value,
        string? notes)
        : base(id)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Bonus value must be greater than zero.", nameof(value));
        }

        if (amountType == PayrollBonusAmountType.Percentage && value > 100)
        {
            throw new ArgumentException("Percentage value must be between 0 and 100.", nameof(value));
        }

        CompanyId = companyId;
        EmployeeId = employeeId;
        ConceptId = conceptId;
        AmountType = amountType;
        Value = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        Notes = NormalizeOptional(notes);
        Status = PayrollBonusStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public static PayrollBonus Create(
        CompanyId companyId,
        EmployeeId employeeId,
        PayrollBonusConceptId conceptId,
        PayrollBonusAmountType amountType,
        decimal value,
        string? notes)
    {
        return new PayrollBonus(PayrollBonusId.New(), companyId, employeeId, conceptId, amountType, value, notes);
    }

    public decimal Resolve(decimal employeeBaseSalary) =>
        AmountType == PayrollBonusAmountType.FixedAmount
            ? Value
            : decimal.Round(employeeBaseSalary * Value / 100m, 2, MidpointRounding.AwayFromZero);

    public void Apply(PayrollLiquidationId liquidationId)
    {
        if (Status != PayrollBonusStatus.Pending)
        {
            throw new InvalidOperationException("Only pending bonuses can be applied.");
        }

        Status = PayrollBonusStatus.Applied;
        PayrollLiquidationId = liquidationId;
    }

    public void Cancel()
    {
        if (Status != PayrollBonusStatus.Pending)
        {
            throw new InvalidOperationException("Only pending bonuses can be cancelled.");
        }

        Status = PayrollBonusStatus.Cancelled;
    }

    public void RevertToPending()
    {
        if (Status != PayrollBonusStatus.Applied)
        {
            throw new InvalidOperationException("Only applied bonuses can be reverted.");
        }

        Status = PayrollBonusStatus.Pending;
        PayrollLiquidationId = null;
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

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter PayrollBonusTests`
Expected: PASS (12/12)

- [ ] **Step 7: Commit**

```bash
git add eiti.Domain/Payroll/PayrollBonusId.cs eiti.Domain/Payroll/PayrollBonusAmountType.cs eiti.Domain/Payroll/PayrollBonusStatus.cs eiti.Domain/Payroll/PayrollBonus.cs eiti.Tests/PayrollBonusTests.cs
git commit -m "feat(payroll): agregar aggregate PayrollBonus"
```

---

### Task 3: Domain — `PayrollLiquidationBonusLine` + `PayrollLiquidation` changes

**Files:**
- Create: `eiti.Domain/Payroll/PayrollLiquidationBonusLine.cs`
- Modify: `eiti.Domain/Payroll/PayrollLiquidation.cs`
- Test: `eiti.Tests/PayrollLiquidationTests.cs` (existing file — add cases, do not remove existing ones)

**Interfaces:**
- Consumes: `PayrollBonusAmountType` (Task 2), `PayrollLiquidationId` (existing).
- Produces: `PayrollLiquidationBonusLine.Create(Guid payrollBonusId, string conceptName, PayrollBonusAmountType amountType, decimal value, decimal amount)`; `PayrollLiquidation.Create(...)` gains a trailing `IReadOnlyList<PayrollLiquidationBonusLine> bonusLines` parameter; `PayrollLiquidation.BonusLines` read-only collection; `NetAmount` now includes `+ bonusLines`.

- [ ] **Step 1: Write the failing tests**

`eiti.Tests/PayrollLiquidationTests.cs` has a private helper `CreateLiquidation(decimal grossAmount, IReadOnlyList<PayrollLiquidationDeductionLine>? deductions, IReadOnlyList<PayrollLiquidationAdvanceLine>? advances)` (lines 10-25) that calls `PayrollLiquidation.Create(...)` with `deductions ?? []` and `advances ?? []` as the last two arguments. Extend it with a third optional list, keeping every existing call site (which omits it) compiling unchanged:

```csharp
    private static PayrollLiquidation CreateLiquidation(
        decimal grossAmount = 500000m,
        IReadOnlyList<PayrollLiquidationDeductionLine>? deductions = null,
        IReadOnlyList<PayrollLiquidationAdvanceLine>? advances = null,
        IReadOnlyList<PayrollLiquidationBonusLine>? bonuses = null)
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
            advances ?? [],
            bonuses ?? []);
    }
```

Then add these cases at the end of the class:

```csharp
    [Fact]
    public void NetAmount_ShouldAddBonusLines_ToGrossAmount()
    {
        var bonusLine = PayrollLiquidationBonusLine.Create(Guid.NewGuid(), "Presentismo", PayrollBonusAmountType.FixedAmount, 15000m, 15000m);

        var liquidation = CreateLiquidation(300000m, bonuses: [bonusLine]);

        liquidation.NetAmount.Should().Be(315000m);
    }

    [Fact]
    public void NetAmount_ShouldCombineBonusesDeductionsAndAdvances()
    {
        var bonusLine = PayrollLiquidationBonusLine.Create(Guid.NewGuid(), "Presentismo", PayrollBonusAmountType.Percentage, 10m, 30000m);
        var deductionLine = PayrollLiquidationDeductionLine.Create("Jubilacion", 11m, 33000m);
        var advanceLine = PayrollLiquidationAdvanceLine.Create(Guid.NewGuid(), 20000m);

        var liquidation = CreateLiquidation(300000m, deductions: [deductionLine], advances: [advanceLine], bonuses: [bonusLine]);

        // 300000 + 30000 - 33000 - 20000
        liquidation.NetAmount.Should().Be(277000m);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter PayrollLiquidationTests`
Expected: FAIL (compile error — no `PayrollLiquidationBonusLine`, `Create` doesn't accept a 4th list)

- [ ] **Step 3: Implement `PayrollLiquidationBonusLine`**

```csharp
// eiti.Domain/Payroll/PayrollLiquidationBonusLine.cs
namespace eiti.Domain.Payroll;

public sealed class PayrollLiquidationBonusLine
{
    public Guid Id { get; private set; }
    public PayrollLiquidationId PayrollLiquidationId { get; private set; } = null!;
    public Guid PayrollBonusId { get; private set; }
    public string ConceptName { get; private set; } = string.Empty;
    public PayrollBonusAmountType AmountType { get; private set; }
    public decimal Value { get; private set; }
    public decimal Amount { get; private set; }

    private PayrollLiquidationBonusLine()
    {
    }

    private PayrollLiquidationBonusLine(Guid id, Guid payrollBonusId, string conceptName, PayrollBonusAmountType amountType, decimal value, decimal amount)
    {
        Id = id;
        PayrollBonusId = payrollBonusId;
        ConceptName = conceptName.Trim();
        AmountType = amountType;
        Value = value;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static PayrollLiquidationBonusLine Create(Guid payrollBonusId, string conceptName, PayrollBonusAmountType amountType, decimal value, decimal amount)
    {
        return new PayrollLiquidationBonusLine(Guid.NewGuid(), payrollBonusId, conceptName, amountType, value, amount);
    }

    internal void AttachToLiquidation(PayrollLiquidationId liquidationId)
    {
        PayrollLiquidationId = liquidationId;
    }
}
```

- [ ] **Step 4: Modify `PayrollLiquidation`**

In `eiti.Domain/Payroll/PayrollLiquidation.cs`:

Add the backing field and public collection, next to the existing two:

```csharp
    private readonly List<PayrollLiquidationBonusLine> _bonusLines = [];
    public IReadOnlyCollection<PayrollLiquidationBonusLine> BonusLines => _bonusLines;
```

Change `NetAmount` to:

```csharp
    public decimal NetAmount =>
        GrossAmount
        + _bonusLines.Sum(l => l.Amount)
        - _deductionLines.Sum(l => l.Amount)
        - _advanceLines.Sum(l => l.Amount);
```

Add the parameter to the private constructor (after `advanceLines`) and attach it the same way as the other two lists:

```csharp
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
        IReadOnlyList<PayrollLiquidationAdvanceLine> advanceLines,
        IReadOnlyList<PayrollLiquidationBonusLine> bonusLines)
        : base(id)
    {
        // ... existing validation and assignments unchanged ...

        foreach (var line in deductionLines)
        {
            line.AttachToLiquidation(Id);
            _deductionLines.Add(line);
        }

        foreach (var line in advanceLines)
        {
            line.AttachToLiquidation(Id);
            _advanceLines.Add(line);
        }

        foreach (var line in bonusLines)
        {
            line.AttachToLiquidation(Id);
            _bonusLines.Add(line);
        }
    }
```

And the static factory:

```csharp
    public static PayrollLiquidation Create(
        CompanyId companyId,
        EmployeeId employeeId,
        BranchId? branchId,
        string periodLabel,
        DateTime periodStart,
        DateTime periodEnd,
        decimal grossAmount,
        IReadOnlyList<PayrollLiquidationDeductionLine> deductionLines,
        IReadOnlyList<PayrollLiquidationAdvanceLine> advanceLines,
        IReadOnlyList<PayrollLiquidationBonusLine> bonusLines)
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
            advanceLines,
            bonusLines);
    }
```

Leave every other member of the class untouched.

- [ ] **Step 5: Fix call sites broken by the new required parameter**

Exactly 5 pre-existing call sites pass `deductionLines`/`advanceLines` positionally and need a trailing `[]` added (the `PayrollLiquidationTests.cs` helper was already handled in Step 1 above — skip it here):

- `eiti.Application/Features/Payroll/Liquidations/GeneratePayrollPeriod/GeneratePayrollPeriodHandler.cs` — the `PayrollLiquidation.Create(...)` call inside the `foreach` loop. Add `[]` as the trailing argument (Task 8 replaces this `[]` with real `bonusLines`).
- `eiti.Tests/CancelLiquidationHandlerTests.cs:33` — `..., 500000m, [], [advanceLine]);` → add `, []` before the closing paren.
- `eiti.Tests/CancelLiquidationHandlerTests.cs:90` — `..., 500000m, [], []);` → add `, []` before the closing paren.
- `eiti.Tests/LiquidationQueriesTests.cs:35` — `..., 500000m, [], []);` → add `, []` before the closing paren.
- `eiti.Tests/PayLiquidationHandlerTests.cs:26` — `..., grossAmount, [], []);` → add `, []` before the closing paren.

Line numbers are as of this plan's writing — re-locate by searching `PayrollLiquidation.Create(` if any have shifted.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter PayrollLiquidationTests`
Expected: PASS (all existing cases + the 2 new ones)

Run the full suite to confirm nothing else broke from the signature change: `dotnet test eiti.Tests/eiti.Tests.csproj`
Expected: PASS (all)

- [ ] **Step 7: Commit**

```bash
git add eiti.Domain/Payroll/PayrollLiquidationBonusLine.cs eiti.Domain/Payroll/PayrollLiquidation.cs eiti.Tests/
git commit -m "feat(payroll): bonificaciones suman al neto de la liquidacion"
```

---

### Task 4: Infrastructure — EF configurations + migration

**Files:**
- Create: `eiti.Infrastructure/Persistence/Configurations/PayrollBonusConceptConfiguration.cs`
- Create: `eiti.Infrastructure/Persistence/Configurations/PayrollBonusConfiguration.cs`
- Modify: `eiti.Infrastructure/Persistence/Configurations/PayrollLiquidationConfiguration.cs`
- Modify: `eiti.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: migration `AddPayrollBonuses` (generated)

**Interfaces:**
- Consumes: `PayrollBonusConcept`, `PayrollBonus`, `PayrollLiquidationBonusLine` (Tasks 1-3).
- Produces: `ApplicationDbContext.PayrollBonusConcepts`, `ApplicationDbContext.PayrollBonuses` (`DbSet<T>`); tables `PayrollBonusConcepts`, `PayrollBonuses`, `PayrollLiquidationBonusLines`.

This task has no unit tests of its own (EF configuration is verified by the migration applying cleanly and by Task 5/9's integration-level handler tests). Steps:

- [ ] **Step 1: Add the EF configuration for `PayrollBonusConcept`**

```csharp
// eiti.Infrastructure/Persistence/Configurations/PayrollBonusConceptConfiguration.cs
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PayrollBonusConceptConfiguration : IEntityTypeConfiguration<PayrollBonusConcept>
{
    public void Configure(EntityTypeBuilder<PayrollBonusConcept> builder)
    {
        builder.ToTable("PayrollBonusConcepts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new PayrollBonusConceptId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.IsActive });
    }
}
```

- [ ] **Step 2: Add the EF configuration for `PayrollBonus`**

```csharp
// eiti.Infrastructure/Persistence/Configurations/PayrollBonusConfiguration.cs
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PayrollBonusConfiguration : IEntityTypeConfiguration<PayrollBonus>
{
    public void Configure(EntityTypeBuilder<PayrollBonus> builder)
    {
        builder.ToTable("PayrollBonuses");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new PayrollBonusId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.EmployeeId).HasConversion(id => id.Value, value => new EmployeeId(value)).IsRequired();
        builder.Property(x => x.ConceptId).HasConversion(id => id.Value, value => new PayrollBonusConceptId(value)).IsRequired();
        builder.Property(x => x.AmountType).HasConversion<int>().IsRequired();
        builder.Property(x => x.Value).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.PayrollLiquidationId)
            .HasConversion(id => id!.Value, value => new PayrollLiquidationId(value))
            .IsRequired(false);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.Status });
    }
}

public sealed class PayrollLiquidationBonusLineConfiguration : IEntityTypeConfiguration<PayrollLiquidationBonusLine>
{
    public void Configure(EntityTypeBuilder<PayrollLiquidationBonusLine> builder)
    {
        builder.ToTable("PayrollLiquidationBonusLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PayrollLiquidationId)
            .HasConversion(id => id.Value, value => new PayrollLiquidationId(value))
            .IsRequired();
        builder.Property(x => x.PayrollBonusId).IsRequired();
        builder.Property(x => x.ConceptName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AmountType).HasConversion<int>().IsRequired();
        builder.Property(x => x.Value).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
    }
}
```

Note: `PayrollLiquidationBonusLineConfiguration` goes in the same file as `PayrollBonusConfiguration` — this mirrors how `PayrollLiquidationAdvanceLineConfiguration` lives in `PayrollLiquidationConfiguration.cs` today, but since this line type belongs conceptually with `PayrollBonus`, put it in `PayrollBonusConfiguration.cs` instead. Either file works for EF; keep them together for readability.

- [ ] **Step 3: Wire the new navigation on `PayrollLiquidationConfiguration`**

In `eiti.Infrastructure/Persistence/Configurations/PayrollLiquidationConfiguration.cs`, inside `PayrollLiquidationConfiguration.Configure`, add after the existing `AdvanceLines` `HasMany`:

```csharp
        builder.HasMany(x => x.BonusLines)
            .WithOne()
            .HasForeignKey(l => l.PayrollLiquidationId)
            .OnDelete(DeleteBehavior.Cascade);
```

And add to the `Navigation(...).UsePropertyAccessMode(...)` block:

```csharp
        builder.Navigation(x => x.BonusLines).UsePropertyAccessMode(PropertyAccessMode.Field);
```

- [ ] **Step 4: Register the new `DbSet`s**

In `eiti.Infrastructure/Persistence/ApplicationDbContext.cs`, find the existing `DbSet<PayrollDeductionConcept> PayrollDeductionConcepts` and `DbSet<PayrollAdvance> PayrollAdvances` declarations and add next to them:

```csharp
    public DbSet<PayrollBonusConcept> PayrollBonusConcepts => Set<PayrollBonusConcept>();
    public DbSet<PayrollBonus> PayrollBonuses => Set<PayrollBonus>();
```

(Match whichever exact declaration style — auto-property `Set<T>()` vs. field-backed — the existing `PayrollDeductionConcepts`/`PayrollAdvances` properties use; copy it verbatim.)

- [ ] **Step 5: Generate the migration**

Ensure the locally running `eiti.Api` process is stopped (or ask the user to stop it) so its DLLs aren't locked. Run from `eiti.Infrastructure`:

```bash
dotnet ef migrations add AddPayrollBonuses --startup-project ../eiti.Api --project . -o Migrations
```

If the Api process is still locked, run design-time directly against Infrastructure instead (per the project's documented fallback): use `ApplicationDbContextFactory` — same approach used for `AddPayrollAdvanceCashSessionId`.

- [ ] **Step 6: Verify the migration compiles and matches the model**

Run: `dotnet build eiti.Infrastructure/eiti.Infrastructure.csproj`
Expected: 0 errors.

Inspect the generated migration file — confirm it creates exactly three tables (`PayrollBonusConcepts`, `PayrollBonuses`, `PayrollLiquidationBonusLines`) with the columns/indexes described above, and no unrelated model-snapshot drift from other in-flight changes. If it does include an unrelated pending model change, stop and flag it — don't silently include it.

- [ ] **Step 7: Commit**

```bash
git add eiti.Infrastructure/Persistence/Configurations/PayrollBonusConceptConfiguration.cs eiti.Infrastructure/Persistence/Configurations/PayrollBonusConfiguration.cs eiti.Infrastructure/Persistence/Configurations/PayrollLiquidationConfiguration.cs eiti.Infrastructure/Persistence/ApplicationDbContext.cs eiti.Infrastructure/Migrations/
git commit -m "feat(payroll): configuraciones EF + migracion AddPayrollBonuses"
```

---

### Task 5: Repositories

**Files:**
- Create: `eiti.Application/Abstractions/Repositories/IPayrollBonusConceptRepository.cs`
- Create: `eiti.Application/Abstractions/Repositories/IPayrollBonusRepository.cs`
- Create: `eiti.Infrastructure/Persistence/Repositories/PayrollBonusConceptRepository.cs`
- Create: `eiti.Infrastructure/Persistence/Repositories/PayrollBonusRepository.cs`
- Modify: `eiti.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Produces: `IPayrollBonusConceptRepository.{AddAsync, GetByIdAsync, ListByCompanyAsync}`; `IPayrollBonusRepository.{AddAsync, GetByIdAsync, ListByCompanyAsync, ListPendingByEmployeeAsync}` — same shapes as the deduction-concept and advance repositories respectively, so Task 7/8/9 handlers can consume them without surprises.

No new unit tests in this task — repositories are exercised indirectly by the handler tests in Tasks 7-9 (same convention already used for `PayrollAdvanceRepository`/`PayrollDeductionConceptRepository`, which also have no dedicated repository-level tests).

- [ ] **Step 1: `IPayrollBonusConceptRepository`**

```csharp
// eiti.Application/Abstractions/Repositories/IPayrollBonusConceptRepository.cs
using eiti.Domain.Companies;
using eiti.Domain.Payroll;

namespace eiti.Application.Abstractions.Repositories;

public interface IPayrollBonusConceptRepository
{
    Task AddAsync(PayrollBonusConcept concept, CancellationToken cancellationToken = default);
    Task<PayrollBonusConcept?> GetByIdAsync(PayrollBonusConceptId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollBonusConcept>> ListByCompanyAsync(CompanyId companyId, bool activeOnly, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: `IPayrollBonusRepository`**

```csharp
// eiti.Application/Abstractions/Repositories/IPayrollBonusRepository.cs
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;

namespace eiti.Application.Abstractions.Repositories;

public interface IPayrollBonusRepository
{
    Task AddAsync(PayrollBonus bonus, CancellationToken cancellationToken = default);
    Task<PayrollBonus?> GetByIdAsync(PayrollBonusId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollBonus>> ListByCompanyAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        PayrollBonusStatus? status,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollBonus>> ListPendingByEmployeeAsync(CompanyId companyId, EmployeeId employeeId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: `PayrollBonusConceptRepository`**

```csharp
// eiti.Infrastructure/Persistence/Repositories/PayrollBonusConceptRepository.cs
using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using eiti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class PayrollBonusConceptRepository : IPayrollBonusConceptRepository
{
    private readonly ApplicationDbContext _context;

    public PayrollBonusConceptRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PayrollBonusConcept concept, CancellationToken cancellationToken = default)
    {
        await _context.PayrollBonusConcepts.AddAsync(concept, cancellationToken);
    }

    public async Task<PayrollBonusConcept?> GetByIdAsync(PayrollBonusConceptId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollBonusConcepts
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollBonusConcept>> ListByCompanyAsync(CompanyId companyId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollBonusConcepts.Where(x => x.CompanyId == companyId);

        if (activeOnly)
            query = query.Where(x => x.IsActive);

        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: `PayrollBonusRepository`**

```csharp
// eiti.Infrastructure/Persistence/Repositories/PayrollBonusRepository.cs
using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using eiti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class PayrollBonusRepository : IPayrollBonusRepository
{
    private readonly ApplicationDbContext _context;

    public PayrollBonusRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PayrollBonus bonus, CancellationToken cancellationToken = default)
    {
        await _context.PayrollBonuses.AddAsync(bonus, cancellationToken);
    }

    public async Task<PayrollBonus?> GetByIdAsync(PayrollBonusId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollBonuses
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollBonus>> ListByCompanyAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        PayrollBonusStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollBonuses.Where(x => x.CompanyId == companyId);

        if (employeeId is not null)
            query = query.Where(x => x.EmployeeId == employeeId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollBonus>> ListPendingByEmployeeAsync(CompanyId companyId, EmployeeId employeeId, CancellationToken cancellationToken = default)
    {
        // Tracked (sin AsNoTracking): el batch de liquidacion marca estos bonos como
        // Applied en el mismo SaveChanges que crea la liquidacion.
        return await _context.PayrollBonuses
            .Where(x => x.CompanyId == companyId && x.EmployeeId == employeeId && x.Status == PayrollBonusStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Register in DI**

In `eiti.Infrastructure/DependencyInjection.cs`, next to the existing `services.AddScoped<IPayrollAdvanceRepository, PayrollAdvanceRepository>();` line, add:

```csharp
        services.AddScoped<IPayrollBonusConceptRepository, PayrollBonusConceptRepository>();
        services.AddScoped<IPayrollBonusRepository, PayrollBonusRepository>();
```

- [ ] **Step 6: Build to verify**

Run: `dotnet build eiti.Infrastructure/eiti.Infrastructure.csproj`
Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add eiti.Application/Abstractions/Repositories/IPayrollBonusConceptRepository.cs eiti.Application/Abstractions/Repositories/IPayrollBonusRepository.cs eiti.Infrastructure/Persistence/Repositories/PayrollBonusConceptRepository.cs eiti.Infrastructure/Persistence/Repositories/PayrollBonusRepository.cs eiti.Infrastructure/DependencyInjection.cs
git commit -m "feat(payroll): repositorios de bonificaciones + registro DI"
```

---

### Task 6: Application — Bonus concepts CRUD

**Files:**
- Create: `eiti.Application/Features/Payroll/BonusConcepts/BonusConceptResponse.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Commands/CreateBonusConcept/CreateBonusConceptCommand.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Commands/CreateBonusConcept/CreateBonusConceptErrors.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Commands/CreateBonusConcept/CreateBonusConceptHandler.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Commands/CreateBonusConcept/CreateBonusConceptValidator.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Commands/UpdateBonusConcept/UpdateBonusConceptCommand.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Commands/UpdateBonusConcept/UpdateBonusConceptErrors.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Commands/UpdateBonusConcept/UpdateBonusConceptHandler.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Commands/UpdateBonusConcept/UpdateBonusConceptValidator.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Commands/SetBonusConceptActive/SetBonusConceptActiveCommand.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Commands/SetBonusConceptActive/SetBonusConceptActiveErrors.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Commands/SetBonusConceptActive/SetBonusConceptActiveHandler.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Queries/ListBonusConcepts/ListBonusConceptsQuery.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Queries/ListBonusConcepts/ListBonusConceptsErrors.cs`
- Create: `eiti.Application/Features/Payroll/BonusConcepts/Queries/ListBonusConcepts/ListBonusConceptsHandler.cs`
- Test: `eiti.Tests/BonusConceptHandlersTests.cs`

**Interfaces:**
- Consumes: `IPayrollBonusConceptRepository` (Task 5), `PermissionCodes.PayrollManage` (existing).
- Produces: `BonusConceptResponse(Guid Id, string Name, bool IsActive)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// eiti.Tests/BonusConceptHandlersTests.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.SetBonusConceptActive;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.UpdateBonusConcept;
using eiti.Application.Features.Payroll.BonusConcepts.Queries.ListBonusConcepts;
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class BonusConceptHandlersTests
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
        var repository = new Mock<IPayrollBonusConceptRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        PayrollBonusConcept? persisted = null;
        repository
            .Setup(r => r.AddAsync(It.IsAny<PayrollBonusConcept>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollBonusConcept, CancellationToken>((c, _) => persisted = c)
            .Returns(Task.CompletedTask);

        var handler = new CreateBonusConceptHandler(user.Object, repository.Object, unitOfWork.Object);

        var result = await handler.Handle(new CreateBonusConceptCommand("Presentismo"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Presentismo");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateHandler_ShouldFail_WhenConceptNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var repository = new Mock<IPayrollBonusConceptRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollBonusConceptId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollBonusConcept?)null);

        var handler = new UpdateBonusConceptHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new UpdateBonusConceptCommand(Guid.NewGuid(), "Nuevo nombre"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveHandler_ShouldDeactivate_WhenIsActiveFalse()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var concept = PayrollBonusConcept.Create(companyId, "Bonificacion por venta");
        var repository = new Mock<IPayrollBonusConceptRepository>();
        repository
            .Setup(r => r.GetByIdAsync(concept.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(concept);

        var handler = new SetBonusConceptActiveHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new SetBonusConceptActiveCommand(concept.Id.Value, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        concept.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ListHandler_ShouldReturnConcepts_FilteredByActiveOnly()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var concepts = new List<PayrollBonusConcept> { PayrollBonusConcept.Create(companyId, "Presentismo") };
        var repository = new Mock<IPayrollBonusConceptRepository>();
        repository
            .Setup(r => r.ListByCompanyAsync(companyId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(concepts);

        var handler = new ListBonusConceptsHandler(user.Object, repository.Object);

        var result = await handler.Handle(new ListBonusConceptsQuery(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Presentismo");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter BonusConceptHandlersTests`
Expected: FAIL (compile error — types don't exist)

- [ ] **Step 3: Response record**

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/BonusConceptResponse.cs
namespace eiti.Application.Features.Payroll.BonusConcepts;

public sealed record BonusConceptResponse(Guid Id, string Name, bool IsActive);
```

- [ ] **Step 4: `CreateBonusConcept`**

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Commands/CreateBonusConcept/CreateBonusConceptCommand.cs
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.BonusConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;

public sealed record CreateBonusConceptCommand(string Name)
    : IRequest<Result<BonusConceptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
```

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Commands/CreateBonusConcept/CreateBonusConceptErrors.cs
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;

public static class CreateBonusConceptErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.BonusConcepts.Create.Unauthorized",
        "Authentication is required.");
}
```

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Commands/CreateBonusConcept/CreateBonusConceptValidator.cs
using FluentValidation;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;

public sealed class CreateBonusConceptValidator : AbstractValidator<CreateBonusConceptCommand>
{
    public CreateBonusConceptValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}
```

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Commands/CreateBonusConcept/CreateBonusConceptHandler.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.BonusConcepts;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;

public sealed class CreateBonusConceptHandler : IRequestHandler<CreateBonusConceptCommand, Result<BonusConceptResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusConceptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBonusConceptHandler(
        ICurrentUserService currentUserService,
        IPayrollBonusConceptRepository repository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BonusConceptResponse>> Handle(CreateBonusConceptCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BonusConceptResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<BonusConceptResponse>.Failure(CreateBonusConceptErrors.Unauthorized);

        var concept = PayrollBonusConcept.Create(_currentUserService.CompanyId, request.Name);

        await _repository.AddAsync(concept, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BonusConceptResponse>.Success(new BonusConceptResponse(concept.Id.Value, concept.Name, concept.IsActive));
    }
}
```

- [ ] **Step 5: `UpdateBonusConcept`**

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Commands/UpdateBonusConcept/UpdateBonusConceptCommand.cs
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.BonusConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.UpdateBonusConcept;

public sealed record UpdateBonusConceptCommand(Guid Id, string Name)
    : IRequest<Result<BonusConceptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
```

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Commands/UpdateBonusConcept/UpdateBonusConceptErrors.cs
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.UpdateBonusConcept;

public static class UpdateBonusConceptErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.BonusConcepts.Update.Unauthorized",
        "Authentication is required.");

    public static readonly Error NotFound = Error.NotFound(
        "Payroll.BonusConcepts.Update.NotFound",
        "The requested bonus concept was not found.");
}
```

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Commands/UpdateBonusConcept/UpdateBonusConceptValidator.cs
using FluentValidation;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.UpdateBonusConcept;

public sealed class UpdateBonusConceptValidator : AbstractValidator<UpdateBonusConceptCommand>
{
    public UpdateBonusConceptValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}
```

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Commands/UpdateBonusConcept/UpdateBonusConceptHandler.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.BonusConcepts;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.UpdateBonusConcept;

public sealed class UpdateBonusConceptHandler : IRequestHandler<UpdateBonusConceptCommand, Result<BonusConceptResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusConceptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBonusConceptHandler(
        ICurrentUserService currentUserService,
        IPayrollBonusConceptRepository repository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BonusConceptResponse>> Handle(UpdateBonusConceptCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BonusConceptResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<BonusConceptResponse>.Failure(UpdateBonusConceptErrors.Unauthorized);

        var concept = await _repository.GetByIdAsync(new PayrollBonusConceptId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (concept is null)
            return Result<BonusConceptResponse>.Failure(UpdateBonusConceptErrors.NotFound);

        concept.Update(request.Name);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BonusConceptResponse>.Success(new BonusConceptResponse(concept.Id.Value, concept.Name, concept.IsActive));
    }
}
```

- [ ] **Step 6: `SetBonusConceptActive`**

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Commands/SetBonusConceptActive/SetBonusConceptActiveCommand.cs
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.BonusConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.SetBonusConceptActive;

public sealed record SetBonusConceptActiveCommand(Guid Id, bool IsActive)
    : IRequest<Result<BonusConceptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
```

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Commands/SetBonusConceptActive/SetBonusConceptActiveErrors.cs
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.SetBonusConceptActive;

public static class SetBonusConceptActiveErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.BonusConcepts.SetActive.Unauthorized",
        "Authentication is required.");

    public static readonly Error NotFound = Error.NotFound(
        "Payroll.BonusConcepts.SetActive.NotFound",
        "The requested bonus concept was not found.");
}
```

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Commands/SetBonusConceptActive/SetBonusConceptActiveHandler.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.BonusConcepts;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.SetBonusConceptActive;

public sealed class SetBonusConceptActiveHandler : IRequestHandler<SetBonusConceptActiveCommand, Result<BonusConceptResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusConceptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetBonusConceptActiveHandler(
        ICurrentUserService currentUserService,
        IPayrollBonusConceptRepository repository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BonusConceptResponse>> Handle(SetBonusConceptActiveCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BonusConceptResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<BonusConceptResponse>.Failure(SetBonusConceptActiveErrors.Unauthorized);

        var concept = await _repository.GetByIdAsync(new PayrollBonusConceptId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (concept is null)
            return Result<BonusConceptResponse>.Failure(SetBonusConceptActiveErrors.NotFound);

        if (request.IsActive)
            concept.Activate();
        else
            concept.Deactivate();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BonusConceptResponse>.Success(new BonusConceptResponse(concept.Id.Value, concept.Name, concept.IsActive));
    }
}
```

- [ ] **Step 7: `ListBonusConcepts`**

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Queries/ListBonusConcepts/ListBonusConceptsQuery.cs
using eiti.Application.Common;
using eiti.Application.Features.Payroll.BonusConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Queries.ListBonusConcepts;

public sealed record ListBonusConceptsQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<BonusConceptResponse>>>;
```

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Queries/ListBonusConcepts/ListBonusConceptsErrors.cs
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.BonusConcepts.Queries.ListBonusConcepts;

public static class ListBonusConceptsErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.BonusConcepts.List.Unauthorized",
        "Authentication is required.");
}
```

```csharp
// eiti.Application/Features/Payroll/BonusConcepts/Queries/ListBonusConcepts/ListBonusConceptsHandler.cs
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.BonusConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Queries.ListBonusConcepts;

public sealed class ListBonusConceptsHandler : IRequestHandler<ListBonusConceptsQuery, Result<IReadOnlyList<BonusConceptResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusConceptRepository _repository;

    public ListBonusConceptsHandler(ICurrentUserService currentUserService, IPayrollBonusConceptRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<BonusConceptResponse>>> Handle(ListBonusConceptsQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<BonusConceptResponse>>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<IReadOnlyList<BonusConceptResponse>>.Failure(ListBonusConceptsErrors.Unauthorized);

        var concepts = await _repository.ListByCompanyAsync(_currentUserService.CompanyId, request.ActiveOnly, cancellationToken);

        IReadOnlyList<BonusConceptResponse> items = concepts
            .Select(c => new BonusConceptResponse(c.Id.Value, c.Name, c.IsActive))
            .ToList();

        return Result<IReadOnlyList<BonusConceptResponse>>.Success(items);
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter BonusConceptHandlersTests`
Expected: PASS (4/4)

- [ ] **Step 9: Build with dependencies**

Run: `dotnet build eiti.Application/eiti.Application.csproj`
Expected: 0 errors.

- [ ] **Step 10: Commit**

```bash
git add eiti.Application/Features/Payroll/BonusConcepts/ eiti.Tests/BonusConceptHandlersTests.cs
git commit -m "feat(payroll): CRUD de conceptos de bonificacion"
```

---

### Task 7: Application — Bonuses (create, cancel, list)

**Files:**
- Create: `eiti.Application/Features/Payroll/Bonuses/PayrollBonusResponse.cs`
- Create: `eiti.Application/Features/Payroll/Bonuses/Commands/CreatePayrollBonus/CreatePayrollBonusCommand.cs`
- Create: `eiti.Application/Features/Payroll/Bonuses/Commands/CreatePayrollBonus/CreatePayrollBonusErrors.cs`
- Create: `eiti.Application/Features/Payroll/Bonuses/Commands/CreatePayrollBonus/CreatePayrollBonusHandler.cs`
- Create: `eiti.Application/Features/Payroll/Bonuses/Commands/CreatePayrollBonus/CreatePayrollBonusValidator.cs`
- Create: `eiti.Application/Features/Payroll/Bonuses/Commands/CancelPayrollBonus/CancelPayrollBonusCommand.cs`
- Create: `eiti.Application/Features/Payroll/Bonuses/Commands/CancelPayrollBonus/CancelPayrollBonusErrors.cs`
- Create: `eiti.Application/Features/Payroll/Bonuses/Commands/CancelPayrollBonus/CancelPayrollBonusHandler.cs`
- Create: `eiti.Application/Features/Payroll/Bonuses/Queries/ListPayrollBonuses/ListPayrollBonusesQuery.cs`
- Create: `eiti.Application/Features/Payroll/Bonuses/Queries/ListPayrollBonuses/ListPayrollBonusesErrors.cs`
- Create: `eiti.Application/Features/Payroll/Bonuses/Queries/ListPayrollBonuses/ListPayrollBonusesHandler.cs`
- Test: `eiti.Tests/PayrollBonusHandlersTests.cs`

**Interfaces:**
- Consumes: `IPayrollBonusRepository` (Task 5), `IPayrollBonusConceptRepository` (Task 5, to validate `ConceptId` exists), `IEmployeeRepository` (existing, to validate `EmployeeId` exists), `PermissionCodes.PayrollManage`.
- Produces: `PayrollBonusResponse(Guid Id, Guid EmployeeId, Guid ConceptId, int AmountType, decimal Value, string? Notes, int Status, Guid? PayrollLiquidationId)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// eiti.Tests/PayrollBonusHandlersTests.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Bonuses.Commands.CancelPayrollBonus;
using eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;
using eiti.Application.Features.Payroll.Bonuses.Queries.ListPayrollBonuses;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class PayrollBonusHandlersTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        return user;
    }

    [Fact]
    public async Task CreateHandler_ShouldFail_WhenEmployeeNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<EmployeeId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);
        var conceptRepository = new Mock<IPayrollBonusConceptRepository>();
        var bonusRepository = new Mock<IPayrollBonusRepository>();

        var handler = new CreatePayrollBonusHandler(user.Object, bonusRepository.Object, conceptRepository.Object, employeeRepository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreatePayrollBonusCommand(Guid.NewGuid(), Guid.NewGuid(), (int)PayrollBonusAmountType.FixedAmount, 15000m, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateHandler_ShouldFail_WhenConceptNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var employee = Employee.Create(companyId, null, "Juan", "Perez", null, null, null, EmployeeRole.Staff);
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<EmployeeId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        var conceptRepository = new Mock<IPayrollBonusConceptRepository>();
        conceptRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollBonusConceptId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollBonusConcept?)null);
        var bonusRepository = new Mock<IPayrollBonusRepository>();

        var handler = new CreatePayrollBonusHandler(user.Object, bonusRepository.Object, conceptRepository.Object, employeeRepository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreatePayrollBonusCommand(employee.Id.Value, Guid.NewGuid(), (int)PayrollBonusAmountType.FixedAmount, 15000m, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateHandler_ShouldPersistBonus_AndReturnResponse()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var employee = Employee.Create(companyId, null, "Juan", "Perez", null, null, null, EmployeeRole.Staff);
        var concept = PayrollBonusConcept.Create(companyId, "Presentismo");
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<EmployeeId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        var conceptRepository = new Mock<IPayrollBonusConceptRepository>();
        conceptRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollBonusConceptId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(concept);
        var bonusRepository = new Mock<IPayrollBonusRepository>();
        PayrollBonus? persisted = null;
        bonusRepository
            .Setup(r => r.AddAsync(It.IsAny<PayrollBonus>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollBonus, CancellationToken>((b, _) => persisted = b)
            .Returns(Task.CompletedTask);

        var handler = new CreatePayrollBonusHandler(user.Object, bonusRepository.Object, conceptRepository.Object, employeeRepository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreatePayrollBonusCommand(employee.Id.Value, concept.Id.Value, (int)PayrollBonusAmountType.Percentage, 10m, "Julio"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(PayrollBonusStatus.Pending);
        result.Value.AmountType.Should().Be((int)PayrollBonusAmountType.Percentage);
    }

    [Fact]
    public async Task CancelHandler_ShouldCancelPendingBonus()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var bonus = PayrollBonus.Create(companyId, EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.FixedAmount, 15000m, null);
        var bonusRepository = new Mock<IPayrollBonusRepository>();
        bonusRepository
            .Setup(r => r.GetByIdAsync(bonus.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bonus);

        var handler = new CancelPayrollBonusHandler(user.Object, bonusRepository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelPayrollBonusCommand(bonus.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        bonus.Status.Should().Be(PayrollBonusStatus.Cancelled);
    }

    [Fact]
    public async Task CancelHandler_ShouldFail_WhenNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var bonusRepository = new Mock<IPayrollBonusRepository>();
        bonusRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollBonusId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollBonus?)null);

        var handler = new CancelPayrollBonusHandler(user.Object, bonusRepository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelPayrollBonusCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ListHandler_ShouldReturnBonuses()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var bonuses = new List<PayrollBonus> { PayrollBonus.Create(companyId, EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.FixedAmount, 15000m, null) };
        var bonusRepository = new Mock<IPayrollBonusRepository>();
        bonusRepository
            .Setup(r => r.ListByCompanyAsync(companyId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bonuses);

        var handler = new ListPayrollBonusesHandler(user.Object, bonusRepository.Object);

        var result = await handler.Handle(new ListPayrollBonusesQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }
}
```

Before writing this test file, check `Employee.Create(...)`'s exact current parameter order/count in `eiti.Domain/Employees/Employee.cs` (it may differ slightly from the signature guessed above, since a prior session added `BaseSalary`/`PayrollPeriodicity` to it) and adjust the `Employee.Create(...)` calls in the test to match exactly — do not guess.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter PayrollBonusHandlersTests`
Expected: FAIL (compile error — types don't exist)

- [ ] **Step 3: Response record**

```csharp
// eiti.Application/Features/Payroll/Bonuses/PayrollBonusResponse.cs
namespace eiti.Application.Features.Payroll.Bonuses;

public sealed record PayrollBonusResponse(
    Guid Id,
    Guid EmployeeId,
    Guid ConceptId,
    int AmountType,
    decimal Value,
    string? Notes,
    int Status,
    Guid? PayrollLiquidationId);
```

- [ ] **Step 4: `CreatePayrollBonus`**

```csharp
// eiti.Application/Features/Payroll/Bonuses/Commands/CreatePayrollBonus/CreatePayrollBonusCommand.cs
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Bonuses;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;

public sealed record CreatePayrollBonusCommand(
    Guid EmployeeId,
    Guid ConceptId,
    int AmountType,
    decimal Value,
    string? Notes) : IRequest<Result<PayrollBonusResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
```

```csharp
// eiti.Application/Features/Payroll/Bonuses/Commands/CreatePayrollBonus/CreatePayrollBonusErrors.cs
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;

public static class CreatePayrollBonusErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Bonuses.Create.Unauthorized",
        "Authentication is required.");

    public static readonly Error EmployeeNotFound = Error.NotFound(
        "Payroll.Bonuses.Create.EmployeeNotFound",
        "The requested employee was not found.");

    public static readonly Error ConceptNotFound = Error.NotFound(
        "Payroll.Bonuses.Create.ConceptNotFound",
        "The requested bonus concept was not found.");
}
```

```csharp
// eiti.Application/Features/Payroll/Bonuses/Commands/CreatePayrollBonus/CreatePayrollBonusValidator.cs
using FluentValidation;
using eiti.Domain.Payroll;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;

public sealed class CreatePayrollBonusValidator : AbstractValidator<CreatePayrollBonusCommand>
{
    public CreatePayrollBonusValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.ConceptId).NotEmpty();
        RuleFor(x => x.AmountType).Must(value => Enum.IsDefined(typeof(PayrollBonusAmountType), value));
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value)
            .LessThanOrEqualTo(100)
            .WithMessage("Percentage value must be between 0 and 100.")
            .When(x => x.AmountType == (int)PayrollBonusAmountType.Percentage);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
```

```csharp
// eiti.Application/Features/Payroll/Bonuses/Commands/CreatePayrollBonus/CreatePayrollBonusHandler.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Bonuses;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;

public sealed class CreatePayrollBonusHandler : IRequestHandler<CreatePayrollBonusCommand, Result<PayrollBonusResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusRepository _bonusRepository;
    private readonly IPayrollBonusConceptRepository _conceptRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePayrollBonusHandler(
        ICurrentUserService currentUserService,
        IPayrollBonusRepository bonusRepository,
        IPayrollBonusConceptRepository conceptRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _bonusRepository = bonusRepository;
        _conceptRepository = conceptRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollBonusResponse>> Handle(CreatePayrollBonusCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<PayrollBonusResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<PayrollBonusResponse>.Failure(CreatePayrollBonusErrors.Unauthorized);

        var companyId = _currentUserService.CompanyId;

        var employee = await _employeeRepository.GetByIdAsync(new EmployeeId(request.EmployeeId), companyId, cancellationToken);
        if (employee is null)
            return Result<PayrollBonusResponse>.Failure(CreatePayrollBonusErrors.EmployeeNotFound);

        var concept = await _conceptRepository.GetByIdAsync(new PayrollBonusConceptId(request.ConceptId), companyId, cancellationToken);
        if (concept is null)
            return Result<PayrollBonusResponse>.Failure(CreatePayrollBonusErrors.ConceptNotFound);

        var bonus = PayrollBonus.Create(
            companyId, employee.Id, concept.Id, (PayrollBonusAmountType)request.AmountType, request.Value, request.Notes);

        await _bonusRepository.AddAsync(bonus, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollBonusResponse>.Success(new PayrollBonusResponse(
            bonus.Id.Value, bonus.EmployeeId.Value, bonus.ConceptId.Value, (int)bonus.AmountType, bonus.Value, bonus.Notes, (int)bonus.Status, bonus.PayrollLiquidationId?.Value));
    }
}
```

Note: `EmployeeRepository.GetByIdAsync` and `CompanyId` are already non-null-checked above (matching the `CreatePayrollAdvanceHandler` pattern) — do not add a redundant `CompanyId!` null-forgiving operator since `companyId` is already the guarded, non-null value at that point.

- [ ] **Step 5: `CancelPayrollBonus`**

```csharp
// eiti.Application/Features/Payroll/Bonuses/Commands/CancelPayrollBonus/CancelPayrollBonusCommand.cs
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Bonuses;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CancelPayrollBonus;

public sealed record CancelPayrollBonusCommand(Guid Id) : IRequest<Result<PayrollBonusResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
```

```csharp
// eiti.Application/Features/Payroll/Bonuses/Commands/CancelPayrollBonus/CancelPayrollBonusErrors.cs
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CancelPayrollBonus;

public static class CancelPayrollBonusErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Bonuses.Cancel.Unauthorized",
        "Authentication is required.");

    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Bonuses.Cancel.NotFound",
        "The requested bonus was not found.");
}
```

```csharp
// eiti.Application/Features/Payroll/Bonuses/Commands/CancelPayrollBonus/CancelPayrollBonusHandler.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Bonuses;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CancelPayrollBonus;

public sealed class CancelPayrollBonusHandler : IRequestHandler<CancelPayrollBonusCommand, Result<PayrollBonusResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPayrollBonusHandler(
        ICurrentUserService currentUserService,
        IPayrollBonusRepository repository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollBonusResponse>> Handle(CancelPayrollBonusCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<PayrollBonusResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<PayrollBonusResponse>.Failure(CancelPayrollBonusErrors.Unauthorized);

        var bonus = await _repository.GetByIdAsync(new PayrollBonusId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (bonus is null)
            return Result<PayrollBonusResponse>.Failure(CancelPayrollBonusErrors.NotFound);

        try
        {
            bonus.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return Result<PayrollBonusResponse>.Failure(Error.Conflict("Payroll.Bonuses.Cancel.InvalidOperation", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollBonusResponse>.Success(new PayrollBonusResponse(
            bonus.Id.Value, bonus.EmployeeId.Value, bonus.ConceptId.Value, (int)bonus.AmountType, bonus.Value, bonus.Notes, (int)bonus.Status, bonus.PayrollLiquidationId?.Value));
    }
}
```

- [ ] **Step 6: `ListPayrollBonuses`**

```csharp
// eiti.Application/Features/Payroll/Bonuses/Queries/ListPayrollBonuses/ListPayrollBonusesQuery.cs
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Bonuses;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Queries.ListPayrollBonuses;

public sealed record ListPayrollBonusesQuery(Guid? EmployeeId, int? Status) : IRequest<Result<IReadOnlyList<PayrollBonusResponse>>>;
```

```csharp
// eiti.Application/Features/Payroll/Bonuses/Queries/ListPayrollBonuses/ListPayrollBonusesErrors.cs
using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Bonuses.Queries.ListPayrollBonuses;

public static class ListPayrollBonusesErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Bonuses.List.Unauthorized",
        "Authentication is required.");
}
```

```csharp
// eiti.Application/Features/Payroll/Bonuses/Queries/ListPayrollBonuses/ListPayrollBonusesHandler.cs
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Bonuses;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Queries.ListPayrollBonuses;

public sealed class ListPayrollBonusesHandler : IRequestHandler<ListPayrollBonusesQuery, Result<IReadOnlyList<PayrollBonusResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusRepository _repository;

    public ListPayrollBonusesHandler(ICurrentUserService currentUserService, IPayrollBonusRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<PayrollBonusResponse>>> Handle(ListPayrollBonusesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<PayrollBonusResponse>>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<IReadOnlyList<PayrollBonusResponse>>.Failure(ListPayrollBonusesErrors.Unauthorized);

        var employeeId = request.EmployeeId.HasValue ? new EmployeeId(request.EmployeeId.Value) : null;
        var status = request.Status.HasValue ? (PayrollBonusStatus)request.Status.Value : (PayrollBonusStatus?)null;

        var bonuses = await _repository.ListByCompanyAsync(_currentUserService.CompanyId, employeeId, status, cancellationToken);

        IReadOnlyList<PayrollBonusResponse> items = bonuses
            .Select(b => new PayrollBonusResponse(b.Id.Value, b.EmployeeId.Value, b.ConceptId.Value, (int)b.AmountType, b.Value, b.Notes, (int)b.Status, b.PayrollLiquidationId?.Value))
            .ToList();

        return Result<IReadOnlyList<PayrollBonusResponse>>.Success(items);
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter PayrollBonusHandlersTests`
Expected: PASS (6/6)

- [ ] **Step 8: Build with dependencies**

Run: `dotnet build eiti.Application/eiti.Application.csproj`
Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add eiti.Application/Features/Payroll/Bonuses/ eiti.Tests/PayrollBonusHandlersTests.cs
git commit -m "feat(payroll): bonificaciones por empleado (crear, cancelar, listar)"
```

---

### Task 8: Application — wire bonuses into `GeneratePayrollPeriodHandler`

**Files:**
- Modify: `eiti.Application/Features/Payroll/Liquidations/GeneratePayrollPeriod/GeneratePayrollPeriodHandler.cs`
- Test: `eiti.Tests/GeneratePayrollPeriodHandlerTests.cs` (existing file — add cases)

**Interfaces:**
- Consumes: `IPayrollBonusRepository.ListPendingByEmployeeAsync` (Task 5), `PayrollLiquidationBonusLine.Create` (Task 3), `PayrollBonus.Resolve`/`.Apply` (Task 2).

- [ ] **Step 1: Write the failing tests**

`eiti.Tests/GeneratePayrollPeriodHandlerTests.cs` constructs `GeneratePayrollPeriodHandler` with 6 positional arguments today: `user.Object, employeeRepository.Object, deductionRepository.Object, advanceRepository.Object, liquidationRepository.Object, new Mock<IUnitOfWork>().Object`. Step 3 of this task adds `IPayrollBonusRepository` and `IPayrollBonusConceptRepository` as two new constructor parameters (after `advanceRepository`, before `liquidationRepository`) — every existing test's constructor call must get two more `Mock<...>().Object` arguments inserted at that position, or the file won't compile. Add these three new cases, and fix the 4 existing constructor calls (lines ~59-65, ~90-96, ~125-131, ~149-155) by inserting `new Mock<IPayrollBonusRepository>().Object, new Mock<IPayrollBonusConceptRepository>().Object,` after the `advanceRepository.Object,` argument in each:

```csharp
    [Fact]
    public async Task Handle_ShouldAddBonusLines_WhenEmployeeHasPendingBonuses_FixedAndPercentage()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Staff);
        employee.SetPayrollConfig(300000m, PayrollPeriodicity.Monthly);

        var user = MockUser(companyId);
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { employee });

        var concept = PayrollBonusConcept.Create(companyId, "Presentismo");
        var bonusConceptRepository = new Mock<IPayrollBonusConceptRepository>();
        bonusConceptRepository
            .Setup(r => r.GetByIdAsync(concept.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(concept);

        var fixedBonus = PayrollBonus.Create(companyId, employee.Id, concept.Id, PayrollBonusAmountType.FixedAmount, 15000m, null);
        var percentageBonus = PayrollBonus.Create(companyId, employee.Id, concept.Id, PayrollBonusAmountType.Percentage, 10m, null);
        var bonusRepository = new Mock<IPayrollBonusRepository>();
        bonusRepository
            .Setup(r => r.ListPendingByEmployeeAsync(companyId, employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollBonus> { fixedBonus, percentageBonus });

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
            new Mock<IPayrollDeductionConceptRepository>().Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            bonusRepository.Object,
            bonusConceptRepository.Object,
            liquidationRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new GeneratePayrollPeriodCommand((int)PayrollPeriodicity.Monthly, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.BonusLines.Should().HaveCount(2);
        persisted.NetAmount.Should().Be(345000m); // 300000 + 15000 (fijo) + 30000 (10% de 300000)
        fixedBonus.Status.Should().Be(PayrollBonusStatus.Applied);
        fixedBonus.PayrollLiquidationId.Should().Be(persisted.Id);
        percentageBonus.Status.Should().Be(PayrollBonusStatus.Applied);
    }
```

Adjust the constructor-parameter insertion order in Step 3 below to exactly match where you place `bonusRepository`/`bonusConceptRepository` — this test assumes they land right after `advanceRepository` and before `liquidationRepository`, consistent with Step 3's instructions.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter GeneratePayrollPeriodHandlerTests`
Expected: FAIL (compile error — handler constructor doesn't accept a bonus repository yet)

- [ ] **Step 3: Modify `GeneratePayrollPeriodHandler`**

Add the fields, constructor parameters (in this exact order: `advanceRepository`, then `bonusRepository`, then `bonusConceptRepository`, then `liquidationRepository`, then `unitOfWork` — matching Task 8 Step 1's test), and DI wiring:

```csharp
    private readonly IPayrollBonusRepository _bonusRepository;
    private readonly IPayrollBonusConceptRepository _bonusConceptRepository;
```

Add `IPayrollBonusRepository bonusRepository` and `IPayrollBonusConceptRepository bonusConceptRepository` as constructor parameters, positioned after `advanceRepository` and before `liquidationRepository`, and assign both fields.

Inside the `foreach (var employee in ...)` loop, after the existing `pendingAdvances`/`advanceLines` block and before `PayrollLiquidation.Create(...)`, add:

```csharp
            var pendingBonuses = await _bonusRepository.ListPendingByEmployeeAsync(companyId, employee.Id, cancellationToken);
            var bonusConceptNames = new Dictionary<Guid, string>();
            foreach (var bonus in pendingBonuses)
            {
                if (!bonusConceptNames.ContainsKey(bonus.ConceptId.Value))
                {
                    var concept = await _bonusConceptRepository.GetByIdAsync(bonus.ConceptId, companyId, cancellationToken);
                    bonusConceptNames[bonus.ConceptId.Value] = concept?.Name ?? "Bonificacion";
                }
            }
            var bonusLines = pendingBonuses
                .Select(bonus => PayrollLiquidationBonusLine.Create(
                    bonus.Id.Value,
                    bonusConceptNames[bonus.ConceptId.Value],
                    bonus.AmountType,
                    bonus.Value,
                    bonus.Resolve(employee.BaseSalary.Value)))
                .ToList();
```

Update the `PayrollLiquidation.Create(...)` call to pass `bonusLines` as the new trailing argument (Task 3 already added this parameter):

```csharp
            var liquidation = PayrollLiquidation.Create(
                companyId,
                employee.Id,
                employee.BranchId,
                request.PeriodLabel,
                request.PeriodStart,
                request.PeriodEnd,
                employee.BaseSalary.Value,
                deductionLines,
                advanceLines,
                bonusLines);
```

After the existing `foreach (var advance in pendingAdvances) advance.Apply(liquidation.Id);` block, add:

```csharp
            foreach (var bonus in pendingBonuses)
            {
                bonus.Apply(liquidation.Id);
            }
```

- [ ] **Step 4: Register the new dependencies where the handler is constructed**

`GeneratePayrollPeriodHandler` is resolved via MediatR/DI (no manual `new` call in production code), so no controller/DI-registration change is needed beyond Task 5's repository registration — MediatR will inject the two new constructor parameters automatically since both interfaces are already registered in `eiti.Infrastructure/DependencyInjection.cs`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter GeneratePayrollPeriodHandlerTests`
Expected: PASS (all existing + 3 new)

Run the full suite: `dotnet test eiti.Tests/eiti.Tests.csproj`
Expected: PASS (all)

- [ ] **Step 6: Commit**

```bash
git add eiti.Application/Features/Payroll/Liquidations/GeneratePayrollPeriod/GeneratePayrollPeriodHandler.cs eiti.Tests/GeneratePayrollPeriodHandlerTests.cs
git commit -m "feat(payroll): incluir bonificaciones pendientes al generar el periodo"
```

---

### Task 9: Application — revert bonuses on liquidation cancellation + response mapping

**Files:**
- Modify: `eiti.Application/Features/Payroll/Liquidations/CancelLiquidation/CancelLiquidationHandler.cs`
- Modify: `eiti.Application/Features/Payroll/Liquidations/PayrollLiquidationResponse.cs`
- Modify: `eiti.Application/Features/Payroll/Liquidations/PayrollLiquidationMapper.cs`
- Test: `eiti.Tests/CancelLiquidationHandlerTests.cs` (existing file — add cases)

**Interfaces:**
- Consumes: `IPayrollBonusRepository` (Task 5), `PayrollBonus.RevertToPending()` (Task 2), `PayrollLiquidation.BonusLines` (Task 3).
- Produces: `PayrollLiquidationResponse.BonusLines` (new field, same shape as `DeductionLines`/`AdvanceLines`).

- [ ] **Step 1: Write the failing test**

`eiti.Tests/CancelLiquidationHandlerTests.cs` constructs `CancelLiquidationHandler` with 5 positional arguments today: `user.Object, liquidationRepository.Object, advanceRepository.Object, cashSessionRepository.Object, new Mock<IUnitOfWork>().Object` (or `.Object` equivalents). Step 3 of this task adds `IPayrollBonusRepository` as a new constructor parameter, placed after `advanceRepository` and before `cashSessionRepository` — insert `new Mock<IPayrollBonusRepository>().Object,` at that position in all 3 existing tests (lines ~48-53, ~73-78, ~109-114). Note: `PayrollLiquidation.Create(...)` calls in this file also need the `[]` fix from Task 3 Step 5 (lines 33 and 90) — if Task 3 already landed, skip; otherwise apply it now too.

Add this new case:

```csharp
    [Fact]
    public async Task Handle_ShouldRevertAppliedBonuses_ToPending_WhenCancellingLiquidation()
    {
        var companyId = CompanyId.New();
        var employeeId = EmployeeId.New();
        var conceptId = PayrollBonusConceptId.New();
        var bonus = PayrollBonus.Create(companyId, employeeId, conceptId, PayrollBonusAmountType.FixedAmount, 15000m, null);
        var bonusLine = PayrollLiquidationBonusLine.Create(bonus.Id.Value, "Presentismo", PayrollBonusAmountType.FixedAmount, 15000m, 15000m);
        var liquidation = PayrollLiquidation.Create(companyId, employeeId, null, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), 500000m, [], [], [bonusLine]);
        bonus.Apply(liquidation.Id);
        liquidation.MarkAsPaid(PayrollPaymentMethod.Transfer, null);

        var user = MockUser(companyId);
        var liquidationRepository = new Mock<IPayrollLiquidationRepository>();
        liquidationRepository
            .Setup(r => r.GetByIdAsync(liquidation.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(liquidation);

        var bonusRepository = new Mock<IPayrollBonusRepository>();
        bonusRepository
            .Setup(r => r.GetByIdAsync(bonus.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bonus);

        var handler = new CancelLiquidationHandler(
            user.Object,
            liquidationRepository.Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            bonusRepository.Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelLiquidationCommand(liquidation.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        liquidation.Status.Should().Be(PayrollLiquidationStatus.Cancelled);
        bonus.Status.Should().Be(PayrollBonusStatus.Pending);
        bonus.PayrollLiquidationId.Should().BeNull();
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter CancelLiquidationHandlerTests`
Expected: FAIL (compile error — handler constructor doesn't accept a bonus repository yet)

- [ ] **Step 3: Modify `CancelLiquidationHandler`**

Add the field and constructor parameter `IPayrollBonusRepository bonusRepository`, positioned after `advanceRepository` and before `cashSessionRepository` — matching Task 9 Step 1's test — and assign it, same pattern as `_advanceRepository`.

After the existing `foreach (var advanceLine in liquidation.AdvanceLines) { ... advance?.Revert(); }` block (inside the same `try`), add:

```csharp
            foreach (var bonusLine in liquidation.BonusLines)
            {
                var bonus = await _bonusRepository.GetByIdAsync(new PayrollBonusId(bonusLine.PayrollBonusId), companyId, cancellationToken);
                bonus?.RevertToPending();
            }
```

- [ ] **Step 4: Update `PayrollLiquidationResponse` and `PayrollLiquidationMapper`**

```csharp
// eiti.Application/Features/Payroll/Liquidations/PayrollLiquidationResponse.cs
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
    IReadOnlyList<PayrollLiquidationLineResponse> AdvanceLines,
    IReadOnlyList<PayrollLiquidationLineResponse> BonusLines);
```

```csharp
// eiti.Application/Features/Payroll/Liquidations/PayrollLiquidationMapper.cs
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
            liquidation.AdvanceLines.Select(l => new PayrollLiquidationLineResponse("Adelanto", l.Amount)).ToList(),
            liquidation.BonusLines.Select(l => new PayrollLiquidationLineResponse(l.ConceptName, l.Amount)).ToList());
    }
}
```

- [ ] **Step 5: Fix other call sites broken by the new required response field**

Search the solution for `new PayrollLiquidationResponse(` outside `PayrollLiquidationMapper.cs` — there should be none (all production code goes through `PayrollLiquidationMapper.Map`), but check `eiti.Tests/*.cs` for any direct construction and add the new trailing `BonusLines` argument (e.g. `[]`) if found.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter CancelLiquidationHandlerTests`
Expected: PASS (all existing + 1 new)

Run the full suite: `dotnet test eiti.Tests/eiti.Tests.csproj`
Expected: PASS (all)

- [ ] **Step 7: Commit**

```bash
git add eiti.Application/Features/Payroll/Liquidations/CancelLiquidation/CancelLiquidationHandler.cs eiti.Application/Features/Payroll/Liquidations/PayrollLiquidationResponse.cs eiti.Application/Features/Payroll/Liquidations/PayrollLiquidationMapper.cs eiti.Tests/CancelLiquidationHandlerTests.cs
git commit -m "feat(payroll): revertir bonificaciones al cancelar una liquidacion"
```

---

### Task 10: API controllers

**Files:**
- Create: `eiti.Api/Controllers/PayrollBonusConceptsController.cs`
- Create: `eiti.Api/Controllers/PayrollBonusesController.cs`

**Interfaces:**
- Consumes: all commands/queries from Tasks 6-7.

No new unit tests — controllers are thin `ISender.Send(...)` wrappers, consistent with every other controller in this codebase (none of `PayrollAdvancesController`/`PayrollDeductionConceptsController` have dedicated tests either).

- [ ] **Step 1: `PayrollBonusConceptsController`**

```csharp
// eiti.Api/Controllers/PayrollBonusConceptsController.cs
using eiti.Api.Extensions;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.SetBonusConceptActive;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.UpdateBonusConcept;
using eiti.Application.Features.Payroll.BonusConcepts.Queries.ListBonusConcepts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/payroll-bonus-concepts")]
[Authorize]
public sealed class PayrollBonusConceptsController : ControllerBase
{
    private readonly ISender _sender;

    public PayrollBonusConceptsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool activeOnly, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListBonusConceptsQuery(activeOnly), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBonusConceptRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateBonusConceptCommand(request.Name), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBonusConceptRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateBonusConceptCommand(id, request.Name), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetBonusConceptActiveRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetBonusConceptActiveCommand(id, request.IsActive), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record CreateBonusConceptRequest(string Name);
public sealed record UpdateBonusConceptRequest(string Name);
public sealed record SetBonusConceptActiveRequest(bool IsActive);
```

- [ ] **Step 2: `PayrollBonusesController`**

```csharp
// eiti.Api/Controllers/PayrollBonusesController.cs
using eiti.Api.Extensions;
using eiti.Application.Features.Payroll.Bonuses.Commands.CancelPayrollBonus;
using eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;
using eiti.Application.Features.Payroll.Bonuses.Queries.ListPayrollBonuses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/payroll-bonuses")]
[Authorize]
public sealed class PayrollBonusesController : ControllerBase
{
    private readonly ISender _sender;

    public PayrollBonusesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? employeeId, [FromQuery] int? status, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListPayrollBonusesQuery(employeeId, status), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePayrollBonusRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreatePayrollBonusCommand(request.EmployeeId, request.ConceptId, request.AmountType, request.Value, request.Notes),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelPayrollBonusCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record CreatePayrollBonusRequest(Guid EmployeeId, Guid ConceptId, int AmountType, decimal Value, string? Notes);
```

- [ ] **Step 3: Build the whole API project**

Run: `dotnet build eiti.Api/eiti.Api.csproj`
Expected: 0 errors. If MSB3027/MSB3021 file-lock errors appear, ask the user to stop the locally running `eiti.Api` process and rebuild — that is not a compilation error.

- [ ] **Step 4: Commit**

```bash
git add eiti.Api/Controllers/PayrollBonusConceptsController.cs eiti.Api/Controllers/PayrollBonusesController.cs
git commit -m "feat(payroll): controllers de API para bonificaciones"
```

---

### Task 11: Final verification

**Files:** none (verification only).

- [ ] **Step 1: Full solution build**

Run: `dotnet build eiti.Api/eiti.Api.csproj` (builds the whole dependency chain: Domain → Application → Infrastructure → Api)
Expected: 0 errors.

- [ ] **Step 2: Full test suite**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj`
Expected: PASS, 0 failures. Note the total count before/after this plan (should be the pre-existing 127 plus roughly 5 + 12 + 4 + 6 + 3 + 1 = 31 new tests ≈ 158).

- [ ] **Step 3: Confirm migration applies cleanly on the local dev database**

With the API stopped, start it once (`dotnet run --project eiti.Api`) and confirm the startup log shows the `AddPayrollBonuses` migration applying via `Database.Migrate()` with no errors. Stop it again afterward if the user needs the port free.

- [ ] **Step 4: Report**

Summarize: new tables (`PayrollBonusConcepts`, `PayrollBonuses`, `PayrollLiquidationBonusLines`), new endpoints (`/api/payroll-bonus-concepts`, `/api/payroll-bonuses`), new permission usage (`payroll.manage` reused, no new permission codes), and that this plan does not touch the frontend — a follow-up frontend plan is needed before this is usable from the UI.
