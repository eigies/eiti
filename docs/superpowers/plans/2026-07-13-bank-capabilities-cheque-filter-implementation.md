# Bank Capabilities And Cheque Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit bank usage capabilities for card, transfer, and cheque issuer flows, then add cheque-number filtering without duplicating the bank catalog.

**Architecture:** Keep a single `Banks` aggregate and add three usage flags: `UseForCard`, `UseForTransfer`, and `UseForCheque`. User-facing selectors filter by capability, while historical read models keep resolving bank names from all banks so old data remains visible after capability changes.

**Tech Stack:** Backend: .NET 10, MediatR, EF Core, PostgreSQL, xUnit, Moq, FluentAssertions. Frontend: Angular 16 standalone components, Reactive Forms, `SearchableSelectComponent`, existing banks screen design system.

**Worktrees:**
- Backend: `C:\Eiti\worktrees\eiti-bank-capabilities` on `feature/bank-capabilities-cheque-filter`, based on `origin/develop` at `867508f`.
- Frontend: `C:\EiTeFront\worktrees\eiti-front-bank-capabilities` on `feature/bank-capabilities-cheque-filter`, based on `origin/develop` at `7c45710`.

## Global Constraints

- Do not edit the original workspaces `C:\Eiti\eiti` or `C:\EiTeFront\eiti-front`; they contain unrelated user work.
- Keep one bank catalog. Do not add `ChequeBanks`, `ExternalBanks`, or another parallel bank table.
- Capability flags are named exactly `UseForCard`, `UseForTransfer`, and `UseForCheque` in backend contracts, and `useForCard`, `useForTransfer`, and `useForCheque` in frontend models.
- Existing banks must migrate with all three capabilities set to `true`.
- Historical detail/report endpoints must resolve bank names from all banks, not from capability-filtered lists.
- UI copy uses Spanish labels: `Tarjetas`, `Transferencias`, `Cheques`, `Numero de cheque`, `Banco emisor`.
- Frontend styling for Banks must extend `src/app/features/banks/banks.component.css`; do not introduce a new visual system.
- After each task, run the task-specific tests and commit only the files changed by that task.

---

## File Structure

Backend files to modify:

- `eiti.Domain/Banks/Bank.cs`: owns bank capabilities and update rules.
- `eiti.Domain/Banks/BankUsage.cs`: new enum for `All`, `Card`, `Transfer`, `Cheque`.
- `eiti.Infrastructure/Persistence/Configurations/BankConfiguration.cs`: maps new columns with default `true`.
- `eiti.Infrastructure/Persistence/Repositories/BankRepository.cs`: filters list queries by usage.
- `eiti.Application/Abstractions/Repositories/IBankRepository.cs`: extends `ListAsync` signature with optional `BankUsage usage = BankUsage.All` after the existing `CancellationToken`.
- `eiti.Application/Features/Banks/Queries/ListBanks/ListBanksQuery.cs`: adds `BankUsage Usage`.
- `eiti.Application/Features/Banks/Queries/ListBanks/ListBanksHandler.cs`: passes usage and maps response flags.
- `eiti.Application/Features/Banks/Queries/ListBanks/BankResponse.cs`: returns new flags.
- `eiti.Application/Features/Banks/Commands/CreateBank/CreateBankCommand.cs`: accepts usage flags.
- `eiti.Application/Features/Banks/Commands/CreateBank/CreateBankHandler.cs`: creates bank with flags.
- `eiti.Application/Features/Banks/Commands/UpdateBank/UpdateBankCommand.cs`: accepts nullable usage flags.
- `eiti.Application/Features/Banks/Commands/UpdateBank/UpdateBankHandler.cs`: preserves current flags when request omits them.
- `eiti.Application/Features/Banks/Commands/UpsertInstallmentPlan/UpsertInstallmentPlanHandler.cs`: maps response flags.
- `eiti.Api/Controllers/BanksController.cs`: binds `usage` query and flag request bodies.
- `eiti.Application/Features/Banks/Common/BankUsageRules.cs`: new small helper for active/capability validation.
- `eiti.Application/Features/Sales/Commands/CreateSale/CreateSaleErrors.cs`: adds bank usage validation errors.
- `eiti.Application/Features/Sales/Commands/CreateSale/CreateSaleHandler.cs`: validates card, transfer, and cheque bank usage.
- `eiti.Application/Features/Sales/Commands/UpdateSale/UpdateSaleErrors.cs`: adds card/transfer validation errors.
- `eiti.Application/Features/Sales/Commands/UpdateSale/UpdateSaleHandler.cs`: validates card and transfer bank usage.
- `eiti.Application/Features/Customers/Commands/AddCustomerPayment/AddCustomerPaymentErrors.cs`: adds card/cheque validation errors.
- `eiti.Application/Features/Customers/Commands/AddCustomerPayment/AddCustomerPaymentHandler.cs`: validates card and cheque bank usage.
- `eiti.Application/Abstractions/Repositories/IChequeRepository.cs`: adds `Numero` to `ChequeFilters`.
- `eiti.Infrastructure/Persistence/Repositories/ChequeRepository.cs`: filters by cheque number.
- `eiti.Application/Features/Cheques/Queries/ListCheques/ListChequesQuery.cs`: accepts `Numero`.
- `eiti.Application/Features/Cheques/Queries/ListCheques/ListChequesHandler.cs`: passes `Numero`.
- `eiti.Api/Controllers/ChequesController.cs`: binds `numero` query.
- `eiti.Infrastructure/Migrations/*AddBankUsageCapabilities*`: generated EF migration.
- `eiti.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`: generated EF snapshot update.

Backend files to test:

- `eiti.Tests/BankUsageCapabilitiesTests.cs`: new focused tests for bank flags, list filtering, and cheque number filtering.
- `eiti.Tests/CreateSaleHandlerTests.cs`: add handler validation tests for invalid card, transfer, and cheque bank usage.
- `eiti.Tests/AddCustomerPaymentHandlerTests.cs`: new handler validation tests for invalid card and cheque bank usage.

Frontend files to modify:

- `src/app/core/models/bank.models.ts`: adds capability fields and `BankUsage`.
- `src/app/core/services/bank.service.ts`: adds `usage` query and flag payloads.
- `src/app/features/banks/banks.component.ts`: create/edit forms include capability toggles.
- `src/app/features/banks/banks.component.html`: shows 3 toggles and usage chips; card plans depend on `useForCard`.
- `src/app/features/banks/banks.component.css`: styles toggles/chips using the existing screen style.
- `src/app/shared/components/sale-payment-inline/sale-payment-inline.component.ts`: filters card, transfer, and cheque selectors by flags.
- `src/app/features/sales/sales-page.component.ts`: loads active banks with `usage='all'`.
- `src/app/features/sales/sales-full.component.ts`: loads active banks with `usage='all'`.
- `src/app/features/clients/customer-account.component.ts`: filters card and cheque bank options by flags.
- `src/app/features/cheques/cheques.component.ts`: loads cheque banks and sends number filter.
- `src/app/features/cheques/cheques.component.html`: adds cheque number input and labels bank as issuer.
- `src/app/core/models/cheque.models.ts`: adds `numero` to filters.
- `src/app/core/services/cheque.service.ts`: sends `numero` query.
- `src/app/features/cash/cash.component.ts`: explicitly loads `usage='all'` for historical name resolution.

Frontend files to test:

- `src/app/shared/components/sale-payment-inline/sale-payment-inline.component.spec.ts`: verifies selector filtering by capability.

---

### Task 1: Backend Bank Capability Model, Contracts, Repository, And Migration

**Files:**
- Create: `eiti.Domain/Banks/BankUsage.cs`
- Modify: `eiti.Domain/Banks/Bank.cs`
- Modify: `eiti.Infrastructure/Persistence/Configurations/BankConfiguration.cs`
- Modify: `eiti.Application/Abstractions/Repositories/IBankRepository.cs`
- Modify: `eiti.Infrastructure/Persistence/Repositories/BankRepository.cs`
- Modify: `eiti.Application/Features/Banks/Queries/ListBanks/ListBanksQuery.cs`
- Modify: `eiti.Application/Features/Banks/Queries/ListBanks/ListBanksHandler.cs`
- Modify: `eiti.Application/Features/Banks/Queries/ListBanks/BankResponse.cs`
- Modify: `eiti.Application/Features/Banks/Commands/CreateBank/CreateBankCommand.cs`
- Modify: `eiti.Application/Features/Banks/Commands/CreateBank/CreateBankHandler.cs`
- Modify: `eiti.Application/Features/Banks/Commands/UpdateBank/UpdateBankCommand.cs`
- Modify: `eiti.Application/Features/Banks/Commands/UpdateBank/UpdateBankHandler.cs`
- Modify: `eiti.Application/Features/Banks/Commands/UpsertInstallmentPlan/UpsertInstallmentPlanHandler.cs`
- Modify: `eiti.Api/Controllers/BanksController.cs`
- Create: `eiti.Infrastructure/Migrations/<timestamp>_AddBankUsageCapabilities.cs`
- Modify: `eiti.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`
- Test: `eiti.Tests/BankUsageCapabilitiesTests.cs`

**Interfaces:**
- Produces: `BankUsage` enum with values `All = 0`, `Card = 1`, `Transfer = 2`, `Cheque = 3`.
- Produces: `BankResponse(int Id, string Name, bool Active, bool UseForCard, bool UseForTransfer, bool UseForCheque, IReadOnlyList<BankInstallmentPlanResponse> Plans)`.
- Produces: `IBankRepository.ListAsync(bool activeOnly, CompanyId companyId, CancellationToken ct, BankUsage usage = BankUsage.All)`.
- Produces API query: `GET /api/banks?activeOnly=true&usage=card|transfer|cheque|all`.

- [ ] **Step 1: Write failing domain tests for default and update behavior**

Add this file:

```csharp
using eiti.Domain.Banks;
using eiti.Domain.Companies;
using FluentAssertions;

namespace eiti.Tests;

public sealed class BankUsageCapabilitiesTests
{
    [Fact]
    public void Create_ShouldEnableAllCapabilitiesByDefault()
    {
        var bank = Bank.Create(CompanyId.New(), "Banco Galicia");

        bank.UseForCard.Should().BeTrue();
        bank.UseForTransfer.Should().BeTrue();
        bank.UseForCheque.Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldPersistCapabilityFlags()
    {
        var bank = Bank.Create(CompanyId.New(), "Banco Galicia");

        bank.Update("Banco Galicia", active: true, useForCard: false, useForTransfer: true, useForCheque: false);

        bank.UseForCard.Should().BeFalse();
        bank.UseForTransfer.Should().BeTrue();
        bank.UseForCheque.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test eiti.Tests --filter BankUsageCapabilitiesTests --no-restore
```

Expected: compile failure because `UseForCard`, `UseForTransfer`, `UseForCheque`, and the new `Update` signature do not exist.

- [ ] **Step 3: Add `BankUsage` enum**

Create `eiti.Domain/Banks/BankUsage.cs`:

```csharp
namespace eiti.Domain.Banks;

public enum BankUsage
{
    All = 0,
    Card = 1,
    Transfer = 2,
    Cheque = 3
}
```

- [ ] **Step 4: Extend `Bank` aggregate**

In `eiti.Domain/Banks/Bank.cs`, add properties:

```csharp
public bool UseForCard { get; private set; }
public bool UseForTransfer { get; private set; }
public bool UseForCheque { get; private set; }
```

Update constructor and methods:

```csharp
private Bank(CompanyId companyId, string name, bool useForCard, bool useForTransfer, bool useForCheque)
{
    if (string.IsNullOrWhiteSpace(name))
    {
        throw new ArgumentException("Bank name cannot be empty.", nameof(name));
    }

    CompanyId = companyId;
    Name = name.Trim();
    Active = true;
    UseForCard = useForCard;
    UseForTransfer = useForTransfer;
    UseForCheque = useForCheque;
    CreatedAt = DateTime.UtcNow;
    UpdatedAt = DateTime.UtcNow;
}

public static Bank Create(
    CompanyId companyId,
    string name,
    bool useForCard = true,
    bool useForTransfer = true,
    bool useForCheque = true)
{
    return new Bank(companyId, name, useForCard, useForTransfer, useForCheque);
}

public void Update(string name, bool active, bool useForCard, bool useForTransfer, bool useForCheque)
{
    if (string.IsNullOrWhiteSpace(name))
    {
        throw new ArgumentException("Bank name cannot be empty.", nameof(name));
    }

    Name = name.Trim();
    Active = active;
    UseForCard = useForCard;
    UseForTransfer = useForTransfer;
    UseForCheque = useForCheque;
    UpdatedAt = DateTime.UtcNow;
}
```

- [ ] **Step 5: Map EF columns with default `true`**

In `BankConfiguration`, add:

```csharp
builder.Property(b => b.UseForCard)
    .HasDefaultValue(true)
    .IsRequired();

builder.Property(b => b.UseForTransfer)
    .HasDefaultValue(true)
    .IsRequired();

builder.Property(b => b.UseForCheque)
    .HasDefaultValue(true)
    .IsRequired();
```

- [ ] **Step 6: Extend repository interface and implementation**

In `IBankRepository.cs`:

```csharp
Task<IReadOnlyList<Bank>> ListAsync(
    bool activeOnly,
    CompanyId companyId,
    CancellationToken ct,
    BankUsage usage = BankUsage.All);
```

In `BankRepository.ListAsync`, add usage filtering after active filtering:

```csharp
query = usage switch
{
    BankUsage.Card => query.Where(b => b.UseForCard),
    BankUsage.Transfer => query.Where(b => b.UseForTransfer),
    BankUsage.Cheque => query.Where(b => b.UseForCheque),
    _ => query
};
```

- [ ] **Step 7: Extend bank application contracts**

Use these record shapes:

```csharp
public sealed record ListBanksQuery(bool ActiveOnly, BankUsage Usage = BankUsage.All)
    : IRequest<Result<IReadOnlyList<BankResponse>>>;

public sealed record BankResponse(
    int Id,
    string Name,
    bool Active,
    bool UseForCard,
    bool UseForTransfer,
    bool UseForCheque,
    IReadOnlyList<BankInstallmentPlanResponse> Plans);

public sealed record CreateBankCommand(
    string Name,
    bool UseForCard = true,
    bool UseForTransfer = true,
    bool UseForCheque = true)
    : IRequest<Result<BankResponse>>, IRequirePermissions;

public sealed record UpdateBankCommand(
    int Id,
    string Name,
    bool Active,
    bool? UseForCard = null,
    bool? UseForTransfer = null,
    bool? UseForCheque = null)
    : IRequest<Result<BankResponse>>, IRequirePermissions;
```

When mapping `BankResponse`, always include the new flags before `Plans`.

- [ ] **Step 8: Preserve omitted flags in update handler**

In `UpdateBankHandler`, call:

```csharp
bank.Update(
    request.Name,
    request.Active,
    request.UseForCard ?? bank.UseForCard,
    request.UseForTransfer ?? bank.UseForTransfer,
    request.UseForCheque ?? bank.UseForCheque);
```

- [ ] **Step 9: Extend `BanksController` request/response binding**

Use nullable request flags for compatibility:

```csharp
[HttpGet]
public async Task<IActionResult> List(
    [FromQuery] bool activeOnly,
    [FromQuery] BankUsage usage = BankUsage.All,
    CancellationToken cancellationToken = default)
{
    var result = await _sender.Send(new ListBanksQuery(activeOnly, usage), cancellationToken);
    return result.ToActionResult();
}

public sealed record CreateBankRequest(
    string Name,
    bool? UseForCard = null,
    bool? UseForTransfer = null,
    bool? UseForCheque = null);

public sealed record UpdateBankRequest(
    string Name,
    bool Active,
    bool? UseForCard = null,
    bool? UseForTransfer = null,
    bool? UseForCheque = null);
```

Create command call:

```csharp
new CreateBankCommand(
    request.Name,
    request.UseForCard ?? true,
    request.UseForTransfer ?? true,
    request.UseForCheque ?? true)
```

Update command call:

```csharp
new UpdateBankCommand(
    id,
    request.Name,
    request.Active,
    request.UseForCard,
    request.UseForTransfer,
    request.UseForCheque)
```

- [ ] **Step 10: Generate migration**

Run:

```powershell
dotnet ef migrations add AddBankUsageCapabilities --project eiti.Infrastructure --startup-project eiti.Api
```

Expected migration adds non-nullable boolean columns `UseForCard`, `UseForTransfer`, and `UseForCheque` to `Banks` with default `true`.

- [ ] **Step 11: Run focused and full backend tests**

Run:

```powershell
dotnet test eiti.Tests --filter BankUsageCapabilitiesTests --no-restore
dotnet test eiti.Tests --no-restore
```

Expected: all tests pass. Existing warnings about nullable references and `Microsoft.OpenApi` vulnerability may remain.

- [ ] **Step 12: Commit backend bank capabilities**

Run:

```powershell
git status --short
git add eiti.Domain eiti.Application eiti.Infrastructure eiti.Api eiti.Tests
git commit -m "feat: add bank usage capabilities"
```

---

### Task 2: Backend Usage Validation In Payment Flows

**Files:**
- Create: `eiti.Application/Features/Banks/Common/BankUsageRules.cs`
- Modify: `eiti.Application/Features/Sales/Commands/CreateSale/CreateSaleErrors.cs`
- Modify: `eiti.Application/Features/Sales/Commands/CreateSale/CreateSaleHandler.cs`
- Modify: `eiti.Application/Features/Sales/Commands/UpdateSale/UpdateSaleErrors.cs`
- Modify: `eiti.Application/Features/Sales/Commands/UpdateSale/UpdateSaleHandler.cs`
- Modify: `eiti.Application/Features/Customers/Commands/AddCustomerPayment/AddCustomerPaymentErrors.cs`
- Modify: `eiti.Application/Features/Customers/Commands/AddCustomerPayment/AddCustomerPaymentHandler.cs`
- Test: `eiti.Tests/CreateSaleHandlerTests.cs`
- Test: `eiti.Tests/AddCustomerPaymentHandlerTests.cs`

**Interfaces:**
- Consumes: `Bank.UseForCard`, `Bank.UseForTransfer`, `Bank.UseForCheque`.
- Produces: payment handlers reject inactive or unsupported banks before persisting payment/cheque data.

- [ ] **Step 1: Add failing tests for sale bank validation**

In `CreateSaleHandlerTests`, add tests with these names:

```csharp
[Fact]
public async Task Handle_ShouldRejectCardPayment_WhenBankIsNotEnabledForCard()
{
    // Arrange: create active bank with useForCard:false and set CardBankId to that bank.
    // Assert: result.IsFailure and result.Error.Code == "Sales.Create.CardBankInvalid".
}

[Fact]
public async Task Handle_ShouldRejectTransferPayment_WhenBankIsNotEnabledForTransfer()
{
    // Arrange: create active bank with useForTransfer:false and set TransferBankId to that bank.
    // Assert: result.IsFailure and result.Error.Code == "Sales.Create.TransferBankInvalid".
}

[Fact]
public async Task Handle_ShouldRejectChequePayment_WhenBankIsNotEnabledForCheque()
{
    // Arrange: create active bank with useForCheque:false and set Cheque.BankId to that bank.
    // Assert: result.IsFailure and result.Error.Code == "Sales.Create.ChequeBankInvalid".
}
```

Use the existing `CreateSaleHandlerTests` setup pattern: mock `IBankRepository.GetByIdAsync` or `GetByIdsAsync`, create a branch, product, stock, authenticated user, and call `CreateSaleCommand`.

- [ ] **Step 2: Run sale validation tests to verify failure**

Run:

```powershell
dotnet test eiti.Tests --filter "CreateSaleHandlerTests&Bank" --no-restore
```

Expected: tests fail because no validation exists.

- [ ] **Step 3: Add shared bank usage helper**

Create `eiti.Application/Features/Banks/Common/BankUsageRules.cs`:

```csharp
using eiti.Domain.Banks;

namespace eiti.Application.Features.Banks.Common;

public static class BankUsageRules
{
    public static bool Supports(Bank? bank, BankUsage usage)
    {
        if (bank is null || !bank.Active)
            return false;

        return usage switch
        {
            BankUsage.Card => bank.UseForCard,
            BankUsage.Transfer => bank.UseForTransfer,
            BankUsage.Cheque => bank.UseForCheque,
            BankUsage.All => true,
            _ => false
        };
    }
}
```

- [ ] **Step 4: Add sale errors**

In `CreateSaleErrors.cs`:

```csharp
public static readonly Error CardBankInvalid = Error.Validation(
    "Sales.Create.CardBankInvalid",
    "El banco seleccionado no esta habilitado para tarjetas.");

public static readonly Error TransferBankInvalid = Error.Validation(
    "Sales.Create.TransferBankInvalid",
    "El banco seleccionado no esta habilitado para transferencias.");

public static readonly Error ChequeBankInvalid = Error.Validation(
    "Sales.Create.ChequeBankInvalid",
    "El banco seleccionado no esta habilitado como banco emisor de cheques.");
```

In `UpdateSaleErrors.cs`:

```csharp
public static readonly Error CardBankInvalid = Error.Validation(
    "Sales.Update.CardBankInvalid",
    "El banco seleccionado no esta habilitado para tarjetas.");

public static readonly Error TransferBankInvalid = Error.Validation(
    "Sales.Update.TransferBankInvalid",
    "El banco seleccionado no esta habilitado para transferencias.");
```

- [ ] **Step 5: Validate `CreateSaleHandler` bank usage**

In `CreateSaleHandler`, fetch and validate:

```csharp
var transferBankIds = request.Payments
    .Where(p => (SalePaymentMethod)p.IdPaymentMethod == SalePaymentMethod.Transfer && p.TransferBankId.HasValue)
    .Select(p => p.TransferBankId!.Value)
    .Distinct()
    .ToList();

var chequeBankIds = request.Payments
    .Where(p => (SalePaymentMethod)p.IdPaymentMethod == SalePaymentMethod.Check && p.Cheque is not null)
    .Select(p => p.Cheque!.BankId)
    .Distinct()
    .ToList();
```

Use `GetByIdsAsync` to build a bank map containing card, transfer, and cheque ids. Before persisting:

```csharp
if (cardBankIds.Any(id => !bankMap.TryGetValue(id, out var bank) || !BankUsageRules.Supports(bank, BankUsage.Card)))
    return Result<CreateSaleResponse>.Failure(CreateSaleErrors.CardBankInvalid);

if (transferBankIds.Any(id => !bankMap.TryGetValue(id, out var bank) || !BankUsageRules.Supports(bank, BankUsage.Transfer)))
    return Result<CreateSaleResponse>.Failure(CreateSaleErrors.TransferBankInvalid);

if (chequeBankIds.Any(id => !bankMap.TryGetValue(id, out var bank) || !BankUsageRules.Supports(bank, BankUsage.Cheque)))
    return Result<CreateSaleResponse>.Failure(CreateSaleErrors.ChequeBankInvalid);
```

Keep existing card surcharge logic using the same `bankMap`.

- [ ] **Step 6: Validate `UpdateSaleHandler` bank usage**

For card and transfer request lines, validate:

```csharp
if (reqPayment.CardBankId.HasValue)
{
    var bank = await _bankRepository.GetByIdAsync(reqPayment.CardBankId.Value, companyId, cancellationToken);
    if (!BankUsageRules.Supports(bank, BankUsage.Card))
        return Result<UpdateSaleResponse>.Failure(UpdateSaleErrors.CardBankInvalid);
}

if (reqPayment.TransferBankId.HasValue)
{
    var bank = await _bankRepository.GetByIdAsync(reqPayment.TransferBankId.Value, companyId, cancellationToken);
    if (!BankUsageRules.Supports(bank, BankUsage.Transfer))
        return Result<UpdateSaleResponse>.Failure(UpdateSaleErrors.TransferBankInvalid);
}
```

Avoid changing `UpdateSaleCommand`; it does not accept cheque data.

- [ ] **Step 7: Add customer payment errors and validation**

In `AddCustomerPaymentErrors.cs`:

```csharp
public static readonly Error CardBankInvalid = Error.Validation(
    "Customers.AddPayment.CardBankInvalid",
    "El banco seleccionado no esta habilitado para tarjetas.");

public static readonly Error ChequeBankInvalid = Error.Validation(
    "Customers.AddPayment.ChequeBankInvalid",
    "El banco seleccionado no esta habilitado como banco emisor de cheques.");
```

In `AddCustomerPaymentHandler`, before `SetCardData`:

```csharp
var bank = await _bankRepository.GetByIdAsync(command.CardBankId.Value, companyId, cancellationToken);
if (!BankUsageRules.Supports(bank, BankUsage.Card))
    return Result<AddCustomerPaymentResponse>.Failure(AddCustomerPaymentErrors.CardBankInvalid);
```

Before `Cheque.CreateForCcPayment`:

```csharp
var chequeBank = await _bankRepository.GetByIdAsync(c.BankId, companyId, cancellationToken);
if (!BankUsageRules.Supports(chequeBank, BankUsage.Cheque))
    return Result<AddCustomerPaymentResponse>.Failure(AddCustomerPaymentErrors.ChequeBankInvalid);
```

- [ ] **Step 8: Run validation tests**

Run:

```powershell
dotnet test eiti.Tests --filter "CreateSaleHandlerTests|AddCustomerPayment" --no-restore
dotnet test eiti.Tests --no-restore
```

Expected: all tests pass.

- [ ] **Step 9: Commit backend payment validation**

Run:

```powershell
git status --short
git add eiti.Application eiti.Tests
git commit -m "feat: validate bank capabilities in payment flows"
```

---

### Task 3: Backend Cheque Number Filter

**Files:**
- Modify: `eiti.Application/Abstractions/Repositories/IChequeRepository.cs`
- Modify: `eiti.Infrastructure/Persistence/Repositories/ChequeRepository.cs`
- Modify: `eiti.Application/Features/Cheques/Queries/ListCheques/ListChequesQuery.cs`
- Modify: `eiti.Application/Features/Cheques/Queries/ListCheques/ListChequesHandler.cs`
- Modify: `eiti.Api/Controllers/ChequesController.cs`
- Test: `eiti.Tests/ChequeFilterTests.cs`

**Interfaces:**
- Produces API query: `GET /api/cheques?numero=123`.
- Produces repository filter: `ChequeFilters.Numero`.

- [ ] **Step 1: Add failing test for repository filter shape**

Add a unit-level test that asserts a `ChequeFilters` instance can carry a trimmed cheque number:

```csharp
[Fact]
public void ChequeFilters_ShouldCarryNumeroFilter()
{
    var filters = new ChequeFilters(null, null, null, null, "123");

    filters.Numero.Should().Be("123");
}
```

Expected failure: constructor does not accept `Numero`.

- [ ] **Step 2: Extend `ChequeFilters` and query record**

In `IChequeRepository.cs`:

```csharp
public record ChequeFilters(
    ChequeStatus? Estado,
    int? BankId,
    DateTime? FechaVencFrom,
    DateTime? FechaVencTo,
    string? Numero = null);
```

In `ListChequesQuery.cs`:

```csharp
public sealed record ListChequesQuery(
    ChequeStatus? Estado,
    int? BankId,
    DateTime? FechaVencFrom,
    DateTime? FechaVencTo,
    string? Numero
) : IRequest<Result<IReadOnlyList<ChequeListItemResponse>>>, IRequirePermissions
```

- [ ] **Step 3: Apply repository filtering**

In `ChequeRepository.ListAsync`, after bank filtering:

```csharp
if (!string.IsNullOrWhiteSpace(filters.Numero))
{
    var numero = filters.Numero.Trim();
    query = query.Where(c => c.Numero.Contains(numero));
}
```

- [ ] **Step 4: Wire handler and controller**

In `ListChequesHandler`:

```csharp
var filters = new ChequeFilters(
    request.Estado,
    request.BankId,
    request.FechaVencFrom,
    request.FechaVencTo,
    request.Numero);
```

In `ChequesController.List`, add:

```csharp
[FromQuery] string? numero,
```

and send:

```csharp
new ListChequesQuery(chequeStatus, bankId, fechaVencFrom, fechaVencTo, numero)
```

- [ ] **Step 5: Update existing `new ChequeFilters` call sites**

Update `ListCarteraChequesHandler`:

```csharp
var filters = new ChequeFilters(ChequeStatus.EnCartera, null, null, null, null);
```

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test eiti.Tests --filter ChequeFilters --no-restore
dotnet test eiti.Tests --no-restore
```

Expected: all tests pass.

- [ ] **Step 7: Commit cheque filter backend**

Run:

```powershell
git status --short
git add eiti.Application eiti.Infrastructure eiti.Api eiti.Tests
git commit -m "feat: add cheque number filter"
```

---

### Task 4: Frontend Bank Service, Model, And Banks Screen

**Files:**
- Modify in frontend worktree: `src/app/core/models/bank.models.ts`
- Modify: `src/app/core/services/bank.service.ts`
- Modify: `src/app/features/banks/banks.component.ts`
- Modify: `src/app/features/banks/banks.component.html`
- Modify: `src/app/features/banks/banks.component.css`

**Interfaces:**
- Consumes backend `BankResponse` with `useForCard`, `useForTransfer`, `useForCheque`.
- Produces `BankUsage = 'all' | 'card' | 'transfer' | 'cheque'`.
- Produces `BankService.listBanks(activeOnly?: boolean, usage?: BankUsage)`.

- [ ] **Step 1: Update frontend model**

In `bank.models.ts`:

```ts
export type BankUsage = 'all' | 'card' | 'transfer' | 'cheque';

export interface BankResponse {
  id: number;
  name: string;
  active: boolean;
  useForCard: boolean;
  useForTransfer: boolean;
  useForCheque: boolean;
  plans: BankInstallmentPlanResponse[];
}

export interface BankUpsertRequest {
  name: string;
  active?: boolean;
  useForCard: boolean;
  useForTransfer: boolean;
  useForCheque: boolean;
}
```

- [ ] **Step 2: Update bank service**

In `bank.service.ts`:

```ts
listBanks(activeOnly = false, usage: BankUsage = 'all'): Observable<BankResponse[]> {
  return this.http.get<BankResponse[]>(this.base, {
    params: {
      activeOnly: String(activeOnly),
      usage
    }
  });
}

createBank(data: BankUpsertRequest): Observable<BankResponse> {
  return this.http.post<BankResponse>(this.base, data);
}

updateBank(id: number, data: BankUpsertRequest & { active: boolean }): Observable<BankResponse> {
  return this.http.put<BankResponse>(`${this.base}/${id}`, data);
}
```

- [ ] **Step 3: Update Banks forms**

In `banks.component.ts`, create forms:

```ts
this.createForm = this.fb.group({
  name: ['', Validators.required],
  useForCard: [true],
  useForTransfer: [true],
  useForCheque: [true]
});

this.editForm = this.fb.group({
  name: ['', Validators.required],
  active: [true],
  useForCard: [true],
  useForTransfer: [true],
  useForCheque: [true]
});
```

In `startEdit`:

```ts
this.editForm.setValue({
  name: view.bank.name,
  active: view.bank.active,
  useForCard: view.bank.useForCard,
  useForTransfer: view.bank.useForTransfer,
  useForCheque: view.bank.useForCheque
});
```

In `submitCreate`, send:

```ts
const value = this.createForm.value;
this.bankService.createBank({
  name: value.name,
  useForCard: !!value.useForCard,
  useForTransfer: !!value.useForTransfer,
  useForCheque: !!value.useForCheque
})
```

In `toggleActive`, preserve flags:

```ts
this.bankService.updateBank(view.bank.id, {
  name: view.bank.name,
  active: !view.bank.active,
  useForCard: view.bank.useForCard,
  useForTransfer: view.bank.useForTransfer,
  useForCheque: view.bank.useForCheque
})
```

- [ ] **Step 4: Add usage toggles and chips to Banks HTML**

Add this block in create and edit forms:

```html
<div class="usage-toggle-group">
  <label class="toggle-row-inline">
    <input type="checkbox" formControlName="useForCard" />
    <span class="toggle-row-inline__label">Tarjetas</span>
    <span class="toggle-row-inline__track"></span>
  </label>
  <label class="toggle-row-inline">
    <input type="checkbox" formControlName="useForTransfer" />
    <span class="toggle-row-inline__label">Transferencias</span>
    <span class="toggle-row-inline__track"></span>
  </label>
  <label class="toggle-row-inline">
    <input type="checkbox" formControlName="useForCheque" />
    <span class="toggle-row-inline__label">Cheques</span>
    <span class="toggle-row-inline__track"></span>
  </label>
</div>
```

Show chips in each row:

```html
<div class="usage-chip-row">
  <span class="usage-chip" *ngIf="view.bank.useForCard">Tarjetas</span>
  <span class="usage-chip" *ngIf="view.bank.useForTransfer">Transferencias</span>
  <span class="usage-chip" *ngIf="view.bank.useForCheque">Cheques</span>
</div>
```

Wrap plans section with:

```html
<section class="expand-section plans-section" *ngIf="planForms.has(view.bank.id) && view.bank.useForCard">
```

If `!view.bank.useForCard`, show:

```html
<section class="expand-section plans-section plans-section--disabled">
  <span class="field__hint">Los planes de cuotas se habilitan cuando el banco se usa para tarjetas.</span>
</section>
```

- [ ] **Step 5: Add Banks CSS using existing style**

In `banks.component.css`, add:

```css
.usage-toggle-group,
.usage-chip-row {
  display: flex;
  flex-wrap: wrap;
  gap: .65rem;
  align-items: center;
}

.usage-toggle-group {
  align-self: end;
}

.usage-chip {
  display: inline-flex;
  align-items: center;
  min-height: 1.55rem;
  padding: .18rem .48rem;
  border-radius: 999px;
  border: 1px solid color-mix(in srgb, var(--amber) 28%, var(--border-2));
  background: color-mix(in srgb, var(--amber) 8%, transparent);
  color: color-mix(in srgb, var(--amber) 70%, var(--text) 30%);
  font-family: 'DM Mono', monospace;
  font-size: .62rem;
  letter-spacing: .08em;
  text-transform: uppercase;
}

.plans-section--disabled {
  color: var(--text-dim);
}
```

- [ ] **Step 6: Run frontend build**

Run from `C:\EiTeFront\worktrees\eiti-front-bank-capabilities`:

```powershell
npm run build
```

Expected: build passes. Existing warnings about CSS budgets, CommonJS dependencies, and `align-items: end` may remain.

- [ ] **Step 7: Commit frontend bank screen changes**

Run:

```powershell
git status --short
git add src/app/core/models/bank.models.ts src/app/core/services/bank.service.ts src/app/features/banks
git commit -m "feat: configure bank usage capabilities"
```

---

### Task 5: Frontend Selectors By Bank Capability And Cheque Number Filter

**Files:**
- Modify in frontend worktree: `src/app/shared/components/sale-payment-inline/sale-payment-inline.component.ts`
- Modify: `src/app/features/sales/sales-page.component.ts`
- Modify: `src/app/features/sales/sales-full.component.ts`
- Modify: `src/app/features/clients/customer-account.component.ts`
- Modify: `src/app/features/cheques/cheques.component.ts`
- Modify: `src/app/features/cheques/cheques.component.html`
- Modify: `src/app/core/models/cheque.models.ts`
- Modify: `src/app/core/services/cheque.service.ts`
- Modify: `src/app/features/cash/cash.component.ts`
- Test: `src/app/shared/components/sale-payment-inline/sale-payment-inline.component.spec.ts`

**Interfaces:**
- Consumes `BankResponse.useForCard`, `useForTransfer`, `useForCheque`.
- Produces cheque filters `{ numero?: string | null }`.

- [ ] **Step 1: Add failing component test for capability filtering**

In `sale-payment-inline.component.spec.ts`, add:

```ts
it('filters bank options by payment capability', () => {
  fixture.componentInstance.banks = [
    { id: 1, name: 'Card Bank', active: true, useForCard: true, useForTransfer: false, useForCheque: false, plans: [{ id: 1, cuotas: 1, surchargePct: 0, active: true }] },
    { id: 2, name: 'Transfer Bank', active: true, useForCard: false, useForTransfer: true, useForCheque: false, plans: [] },
    { id: 3, name: 'Cheque Bank', active: true, useForCard: false, useForTransfer: false, useForCheque: true, plans: [] }
  ];

  expect(fixture.componentInstance.activeBanksWithPlansOptions.map(o => o.label)).toEqual(['Card Bank']);
  expect(fixture.componentInstance.activeBanksOptions.map(o => o.label)).toEqual(['Transfer Bank']);
  expect(fixture.componentInstance.bankOptions.map(o => o.label)).toEqual(['Cheque Bank']);
});
```

- [ ] **Step 2: Run test to verify failure**

Run:

```powershell
npm test -- --watch=false --browsers=ChromeHeadless --include src/app/shared/components/sale-payment-inline/sale-payment-inline.component.spec.ts
```

Expected: failure because getters ignore usage flags. If local ChromeHeadless is unavailable, record that and continue with `npm run build` after implementation.

- [ ] **Step 3: Update `SalePaymentInlineComponent` filters**

Use these getters:

```ts
get activeBanks(): BankResponse[] {
  return this.banks.filter(b => b.active && b.useForTransfer);
}

get activeBanksWithPlans(): BankResponse[] {
  return this.banks.filter(b => b.active && b.useForCard && b.plans.some(p => p.active));
}

get bankOptions(): SearchableSelectOption[] {
  return this.banks
    .filter(bank => bank.active && bank.useForCheque)
    .map(bank => ({
      value: bank.id,
      label: bank.name
    }));
}
```

Keep `activePlansForBank` using `this.banks` so selected card bank lookup still works.

- [ ] **Step 4: Load all active banks in sales pages**

In `sales-page.component.ts` and `sales-full.component.ts`, change:

```ts
this.bankService.listBanks(true)
```

to:

```ts
this.bankService.listBanks(true, 'all')
```

The shared component handles capability filtering.

- [ ] **Step 5: Filter customer account bank options**

In `customer-account.component.ts`:

```ts
get activeBanksWithPlans(): BankResponse[] {
  return this.banks.filter(b => b.active && b.useForCard && b.plans.some(p => p.active));
}

get chequeBankOptions(): SearchableSelectOption[] {
  return this.banks
    .filter(b => b.active && b.useForCheque)
    .map(b => ({ value: b.id, label: b.name }));
}
```

Change load call to:

```ts
this.bankService.listBanks(true, 'all').subscribe({
```

- [ ] **Step 6: Add cheque number filter model and service query**

In `cheque.models.ts`:

```ts
export interface ChequeFilters {
  estado?: number | null;
  bankId?: number | null;
  fechaVencFrom?: string | null;
  fechaVencTo?: string | null;
  numero?: string | null;
}
```

In `cheque.service.ts`:

```ts
if (filters.numero) params = params.set('numero', filters.numero);
```

- [ ] **Step 7: Update Cheques screen filters**

In `cheques.component.ts`, add form control:

```ts
numero: ['']
```

Load cheque banks:

```ts
this.bankService.listBanks(false, 'cheque').subscribe({
```

Send filter:

```ts
numero: f.numero?.trim() || null
```

In `cheques.component.html`, add a field before vencimiento dates:

```html
<label class="field">
  <span>Numero de cheque</span>
  <input class="control" type="text" formControlName="numero" placeholder="Buscar numero" />
</label>
```

Change filter description from `estado, banco o rango` to `estado, banco emisor, numero o rango`.

- [ ] **Step 8: Make cash name lookup explicit**

In `cash.component.ts`, change:

```ts
this.bankService.listBanks().subscribe({
```

to:

```ts
this.bankService.listBanks(false, 'all').subscribe({
```

This is historical name resolution, not a selector.

- [ ] **Step 9: Run frontend verification**

Run:

```powershell
npm run build
```

If ChromeHeadless is available, also run:

```powershell
npm test -- --watch=false --browsers=ChromeHeadless --include src/app/shared/components/sale-payment-inline/sale-payment-inline.component.spec.ts
```

Expected: build passes. Existing warnings may remain.

- [ ] **Step 10: Commit frontend selector changes**

Run:

```powershell
git status --short
git add src/app/shared/components/sale-payment-inline src/app/features/sales src/app/features/clients src/app/features/cheques src/app/features/cash src/app/core/models/cheque.models.ts src/app/core/services/cheque.service.ts
git commit -m "feat: filter bank selectors by capability"
```

---

### Task 6: Cross-Repo Final Verification And Handoff

**Files:**
- Backend worktree only: this plan file.
- No source modifications beyond earlier committed tasks.

**Interfaces:**
- Produces final verification evidence for both repos.

- [ ] **Step 1: Run backend full verification**

From `C:\Eiti\worktrees\eiti-bank-capabilities`:

```powershell
dotnet test eiti.Tests --no-restore
```

Expected: all backend tests pass. Baseline before implementation was `65` passed, `0` failed.

- [ ] **Step 2: Run frontend full verification**

From `C:\EiTeFront\worktrees\eiti-front-bank-capabilities`:

```powershell
npm run build
```

Expected: build passes. Baseline before implementation passed with warnings for CSS budgets, CommonJS dependencies, and `align-items: end`.

- [ ] **Step 3: Inspect both git histories**

Run in each worktree:

```powershell
git status --short
git log --oneline --decorate -5
```

Expected: source changes are committed, except this plan file if it has not yet been committed.

- [ ] **Step 4: Commit the plan file in backend worktree**

From `C:\Eiti\worktrees\eiti-bank-capabilities`:

```powershell
git add docs/superpowers/plans/2026-07-13-bank-capabilities-cheque-filter-implementation.md
git commit -m "docs: plan bank capabilities implementation"
```

- [ ] **Step 5: Report branches and verification**

Report:

```text
Backend worktree: C:\Eiti\worktrees\eiti-bank-capabilities
Backend branch: feature/bank-capabilities-cheque-filter
Frontend worktree: C:\EiTeFront\worktrees\eiti-front-bank-capabilities
Frontend branch: feature/bank-capabilities-cheque-filter
Backend verification: dotnet test eiti.Tests --no-restore
Frontend verification: npm run build
```

---

## Self-Review Notes

- Spec coverage: bank capabilities, single catalog, selectors by capability, cheque number filter, and historical name resolution are covered.
- No alternate bank table is introduced.
- Existing data compatibility is covered by the EF migration default `true`.
- Backend and frontend work are split into reviewable commits.
- Historical reporting/listing endpoints intentionally continue to resolve bank names from all banks.
