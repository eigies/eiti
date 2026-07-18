# Presupuestos (Quotes) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Presupuestos" (Quotes) feature: a pre-sale quote (with an optional linked customer or free-text prospect) that doesn't touch stock, can be downloaded as a PDF, expires after a configurable date, and can be converted — with edits allowed — into a real CC sale.

**Architecture:** New aggregate `Quote` (backend, `eiti.Domain/Quotes/`) fully independent from `Sale`, with its own vertical-slice features (`Create`, `Cancel`, `ConvertToSale`, `List`, `GetById`). Conversion reuses the existing `CreateCcSaleCommand`/`CreateCcSaleHandler` via `IMediator.Send`, so CC-sale creation logic is never duplicated. Frontend adds a `quotes` feature (list, create form, detail+PDF) and wires the "convert" action into the existing `sales-cc` component via router navigation state, so that component's existing customer/product/discount logic is reused for the edit-before-confirm step.

**Tech Stack:** .NET 10, Clean Architecture, MediatR, FluentValidation, EF Core (PostgreSQL/Npgsql), xUnit + Moq + FluentAssertions · Angular 16 standalone components, Reactive Forms, RxJS, jsPDF.

## Global Constraints

- Backend: Vertical Slice per feature (`eiti.Application/Features/Quotes/<Command|Query>/<Feature>/`), one handler per file, `Result<T>` pattern (never throw for business errors), `EnsureAuthenticated()`/`EnsureAuthenticatedWithContext()` at the top of every handler, `decimal(18,2)` for money columns, always build with dependencies (`dotnet build eiti.Application/eiti.Application.csproj`), never `--no-dependencies` for final verification.
- Permission codes need to land in **three** backend places: `PermissionCodes.cs`, `PermissionCatalog.All`, `RoleCatalog.cs` — plus the frontend `permission.models.ts` mirror.
- Frontend: standalone components only, `OnPush` where feasible, Reactive Forms, typed `HttpClient`, URLs built from `environment.apiUrl`, no `any`, final verification is `cd C:/EiTeFront/eiti-front && ng build --configuration development`.
- The `Quote` aggregate never touches `BranchProductStock` or `StockMovement` — no reservation, no stock read even for pricing (the frontend supplies the price it already resolved from stock/catalog when building the quote).
- `QuoteStatus` has exactly 3 persisted values (`Pending`, `Converted`, `Cancelled`). "Expired" is always derived from `ExpiresAt < now && Status == Pending`, never persisted.

---

## File Structure

**Backend — new files:**
- `eiti.Domain/Quotes/QuoteId.cs`
- `eiti.Domain/Quotes/QuoteStatus.cs`
- `eiti.Domain/Quotes/QuoteDetail.cs`
- `eiti.Domain/Quotes/Quote.cs`
- `eiti.Application/Abstractions/Repositories/IQuoteRepository.cs`
- `eiti.Infrastructure/Persistence/Repositories/QuoteRepository.cs`
- `eiti.Infrastructure/Persistence/Configurations/QuoteConfiguration.cs`
- `eiti.Infrastructure/Persistence/Configurations/QuoteDetailConfiguration.cs`
- `eiti.Application/Features/Quotes/Commands/CreateQuote/{CreateQuoteCommand,CreateQuoteHandler,CreateQuoteValidator,CreateQuoteErrors,CreateQuoteResponse}.cs`
- `eiti.Application/Features/Quotes/Commands/CancelQuote/{CancelQuoteCommand,CancelQuoteHandler,CancelQuoteErrors}.cs`
- `eiti.Application/Features/Quotes/Commands/ConvertQuoteToSale/{ConvertQuoteToSaleCommand,ConvertQuoteToSaleHandler,ConvertQuoteToSaleErrors}.cs`
- `eiti.Application/Features/Quotes/Queries/ListQuotes/{ListQuotesQuery,ListQuotesHandler,QuoteListItemResponse}.cs`
- `eiti.Application/Features/Quotes/Queries/GetQuoteById/{GetQuoteByIdQuery,GetQuoteByIdHandler,QuoteDetailResponse}.cs`
- `eiti.Api/Controllers/QuotesController.cs`
- `eiti.Tests/QuoteTests.cs`
- `eiti.Tests/CreateQuoteHandlerTests.cs`
- `eiti.Tests/ConvertQuoteToSaleHandlerTests.cs`
- New EF migration `AddQuotes`

**Backend — modified files:**
- `eiti.Application/Common/Authorization/PermissionCodes.cs`
- `eiti.Application/Common/Authorization/PermissionCatalog.cs`
- `eiti.Application/Common/Authorization/RoleCatalog.cs`
- `eiti.Infrastructure/Persistence/ApplicationDbContext.cs`

**Frontend — new files:**
- `src/app/core/models/quote.models.ts`
- `src/app/core/services/quote.service.ts`
- `src/app/features/quotes/quotes-list/quotes-list.component.{ts,html,css}`
- `src/app/features/quotes/quote-form/quote-form.component.{ts,html,css}`
- `src/app/features/quotes/quote-detail-modal/quote-detail-modal.component.{ts,html,css}`
- `src/app/shared/utils/quote-pdf.util.ts`

**Frontend — modified files:**
- `src/app/core/models/permission.models.ts`
- `src/app/app.routes.ts`
- `src/app/shared/components/navbar/navbar.component.html`
- `src/app/features/sales/sales-cc/sales-cc.component.ts` (+ `.html`)

---

### Task 1: Domain — `Quote` aggregate, `QuoteDetail`, value objects

**Files:**
- Create: `eiti.Domain/Quotes/QuoteId.cs`
- Create: `eiti.Domain/Quotes/QuoteStatus.cs`
- Create: `eiti.Domain/Quotes/QuoteDetail.cs`
- Create: `eiti.Domain/Quotes/Quote.cs`
- Test: `eiti.Tests/QuoteTests.cs`

**Interfaces:**
- Produces: `QuoteId(Guid Value)` record with `.New()`; `QuoteStatus` enum (`Pending=1, Converted=2, Cancelled=3`); `QuoteDetail.Create(ProductId productId, int quantity, decimal unitPrice, decimal discountPercent = 0)` → `QuoteDetail` with `.ProductId`, `.Quantity`, `.UnitPrice`, `.DiscountPercent`, `.LineTotal`; `Quote.Create(CompanyId, BranchId, CustomerId?, string? prospectName, string? prospectContact, IEnumerable<QuoteDetail>, decimal generalDiscountPercent, DateTime expiresAt, Guid createdByUserId, string? code = null, DateTime? createdAt = null)` → `Quote` with `.Id`, `.CompanyId`, `.BranchId`, `.CustomerId`, `.ProspectName`, `.ProspectContact`, `.Details` (`IReadOnlyCollection<QuoteDetail>`), `.GeneralDiscountPercent`, `.TotalAmount`, `.ExpiresAt`, `.Status`, `.ConvertedSaleId`, `.Code`, `.CreatedByUserId`, `.CreatedAt`, plus methods `.Cancel()`, `.MarkConverted(Guid saleId, DateTime now)`, `.IsExpired(DateTime now)`.

- [ ] **Step 1: Write the failing domain tests**

```csharp
// eiti.Tests/QuoteTests.cs
using eiti.Domain.Companies;
using eiti.Domain.Branches;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using FluentAssertions;

namespace eiti.Tests;

public sealed class QuoteTests
{
    private static QuoteDetail SampleDetail() =>
        QuoteDetail.Create(ProductId.New(), 2, 150m, 10m);

    [Fact]
    public void Create_ShouldThrow_WhenBothCustomerAndProspectProvided()
    {
        var act = () => Quote.Create(
            CompanyId.New(), BranchId.New(), CustomerId.New(),
            prospectName: "Juan Perez", prospectContact: null,
            details: new[] { SampleDetail() },
            generalDiscountPercent: 0,
            expiresAt: DateTime.UtcNow.AddDays(7),
            createdByUserId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNeitherCustomerNorProspectProvided()
    {
        var act = () => Quote.Create(
            CompanyId.New(), BranchId.New(), customerId: null,
            prospectName: null, prospectContact: null,
            details: new[] { SampleDetail() },
            generalDiscountPercent: 0,
            expiresAt: DateTime.UtcNow.AddDays(7),
            createdByUserId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNoDetails()
    {
        var act = () => Quote.Create(
            CompanyId.New(), BranchId.New(), customerId: null,
            prospectName: "Juan Perez", prospectContact: "1122334455",
            details: Array.Empty<QuoteDetail>(),
            generalDiscountPercent: 0,
            expiresAt: DateTime.UtcNow.AddDays(7),
            createdByUserId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldComputeTotal_WithGeneralDiscount()
    {
        var quote = Quote.Create(
            CompanyId.New(), BranchId.New(), customerId: CustomerId.New(),
            prospectName: null, prospectContact: null,
            details: new[] { QuoteDetail.Create(ProductId.New(), 2, 100m) },
            generalDiscountPercent: 10m,
            expiresAt: DateTime.UtcNow.AddDays(7),
            createdByUserId: Guid.NewGuid());

        quote.TotalAmount.Should().Be(180m);
        quote.Status.Should().Be(QuoteStatus.Pending);
    }

    [Fact]
    public void Cancel_ShouldThrow_WhenNotPending()
    {
        var quote = Quote.Create(
            CompanyId.New(), BranchId.New(), customerId: CustomerId.New(),
            prospectName: null, prospectContact: null,
            details: new[] { SampleDetail() },
            generalDiscountPercent: 0,
            expiresAt: DateTime.UtcNow.AddDays(7),
            createdByUserId: Guid.NewGuid());
        quote.Cancel();

        var act = () => quote.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkConverted_ShouldThrow_WhenExpired()
    {
        var quote = Quote.Create(
            CompanyId.New(), BranchId.New(), customerId: CustomerId.New(),
            prospectName: null, prospectContact: null,
            details: new[] { SampleDetail() },
            generalDiscountPercent: 0,
            expiresAt: DateTime.UtcNow.AddDays(1),
            createdByUserId: Guid.NewGuid());

        var act = () => quote.MarkConverted(Guid.NewGuid(), DateTime.UtcNow.AddDays(2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkConverted_ShouldSetConvertedSaleId_WhenPendingAndNotExpired()
    {
        var quote = Quote.Create(
            CompanyId.New(), BranchId.New(), customerId: CustomerId.New(),
            prospectName: null, prospectContact: null,
            details: new[] { SampleDetail() },
            generalDiscountPercent: 0,
            expiresAt: DateTime.UtcNow.AddDays(7),
            createdByUserId: Guid.NewGuid());
        var saleId = Guid.NewGuid();

        quote.MarkConverted(saleId, DateTime.UtcNow);

        quote.Status.Should().Be(QuoteStatus.Converted);
        quote.ConvertedSaleId.Should().Be(saleId);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail (compile error — types don't exist yet)**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter QuoteTests`
Expected: build FAILS — `Quote`, `QuoteDetail`, `QuoteStatus` do not exist.

- [ ] **Step 3: Implement `QuoteId` and `QuoteStatus`**

```csharp
// eiti.Domain/Quotes/QuoteId.cs
namespace eiti.Domain.Quotes;

public sealed record QuoteId(Guid Value)
{
    public static QuoteId New() => new(Guid.NewGuid());
}
```

```csharp
// eiti.Domain/Quotes/QuoteStatus.cs
namespace eiti.Domain.Quotes;

public enum QuoteStatus
{
    Pending = 1,
    Converted = 2,
    Cancelled = 3
}
```

- [ ] **Step 4: Implement `QuoteDetail`**

```csharp
// eiti.Domain/Quotes/QuoteDetail.cs
using eiti.Domain.Products;

namespace eiti.Domain.Quotes;

public sealed class QuoteDetail
{
    public QuoteId QuoteId { get; private set; } = null!;
    public ProductId ProductId { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal LineTotal { get; private set; }

    private QuoteDetail()
    {
    }

    private QuoteDetail(ProductId productId, int quantity, decimal unitPrice, decimal discountPercent)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Quote detail quantity must be greater than zero.", nameof(quantity));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentException("Quote detail unit price cannot be negative.", nameof(unitPrice));
        }

        if (discountPercent < 0 || discountPercent > 100)
        {
            throw new ArgumentException("Discount percent must be between 0 and 100.", nameof(discountPercent));
        }

        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountPercent = decimal.Round(discountPercent, 2, MidpointRounding.AwayFromZero);
        LineTotal = ComputeTotal(quantity, unitPrice, DiscountPercent);
    }

    public static QuoteDetail Create(ProductId productId, int quantity, decimal unitPrice, decimal discountPercent = 0)
    {
        return new QuoteDetail(productId, quantity, unitPrice, discountPercent);
    }

    internal void AttachToQuote(QuoteId quoteId)
    {
        QuoteId = quoteId;
    }

    private static decimal ComputeTotal(int quantity, decimal unitPrice, decimal discountPercent)
    {
        var subtotal = quantity * unitPrice;
        if (discountPercent > 0)
        {
            subtotal *= 1m - discountPercent / 100m;
        }
        return decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero);
    }
}
```

- [ ] **Step 5: Implement `Quote` aggregate**

```csharp
// eiti.Domain/Quotes/Quote.cs
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Primitives;

namespace eiti.Domain.Quotes;

public sealed class Quote : AggregateRoot<QuoteId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public BranchId BranchId { get; private set; } = null!;
    public CustomerId? CustomerId { get; private set; }
    public string? ProspectName { get; private set; }
    public string? ProspectContact { get; private set; }
    public decimal GeneralDiscountPercent { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public QuoteStatus Status { get; private set; }
    public Guid? ConvertedSaleId { get; private set; }
    public string? Code { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<QuoteDetail> _details = [];
    public IReadOnlyCollection<QuoteDetail> Details => _details;

    private Quote()
    {
    }

    private Quote(
        QuoteId id,
        CompanyId companyId,
        BranchId branchId,
        CustomerId? customerId,
        string? prospectName,
        string? prospectContact,
        List<QuoteDetail> details,
        decimal generalDiscountPercent,
        DateTime expiresAt,
        Guid createdByUserId,
        DateTime createdAt,
        string? code)
        : base(id)
    {
        CompanyId = companyId;
        BranchId = branchId;
        CustomerId = customerId;
        ProspectName = prospectName;
        ProspectContact = prospectContact;
        GeneralDiscountPercent = NormalizePercent(generalDiscountPercent);
        ExpiresAt = expiresAt;
        Status = QuoteStatus.Pending;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        Code = code;
        _details = details;

        foreach (var detail in _details)
        {
            detail.AttachToQuote(id);
        }

        RecalculateTotal();
    }

    public static Quote Create(
        CompanyId companyId,
        BranchId branchId,
        CustomerId? customerId,
        string? prospectName,
        string? prospectContact,
        IEnumerable<QuoteDetail> details,
        decimal generalDiscountPercent,
        DateTime expiresAt,
        Guid createdByUserId,
        string? code = null,
        DateTime? createdAt = null)
    {
        var hasCustomer = customerId is not null;
        var hasProspect = !string.IsNullOrWhiteSpace(prospectName);

        if (hasCustomer == hasProspect)
        {
            throw new ArgumentException(
                "A quote must have exactly one of CustomerId or ProspectName.", nameof(customerId));
        }

        var detailList = details.ToList();
        if (detailList.Count == 0)
        {
            throw new ArgumentException("A quote requires at least one detail.", nameof(details));
        }

        var effectiveCreatedAt = createdAt ?? DateTime.UtcNow;
        if (expiresAt <= effectiveCreatedAt)
        {
            throw new ArgumentException("ExpiresAt must be in the future.", nameof(expiresAt));
        }

        return new Quote(
            QuoteId.New(),
            companyId,
            branchId,
            customerId,
            hasProspect ? prospectName!.Trim() : null,
            hasProspect ? prospectContact?.Trim() : null,
            detailList,
            generalDiscountPercent,
            expiresAt,
            createdByUserId,
            effectiveCreatedAt,
            code);
    }

    public bool IsExpired(DateTime now) => Status == QuoteStatus.Pending && ExpiresAt < now;

    public void Cancel()
    {
        if (Status != QuoteStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot cancel a quote in status '{Status}'.");
        }

        Status = QuoteStatus.Cancelled;
    }

    public void MarkConverted(Guid saleId, DateTime now)
    {
        if (Status != QuoteStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot convert a quote in status '{Status}'.");
        }

        if (IsExpired(now))
        {
            throw new InvalidOperationException("Cannot convert an expired quote.");
        }

        Status = QuoteStatus.Converted;
        ConvertedSaleId = saleId;
    }

    private void RecalculateTotal()
    {
        var subtotal = _details.Sum(detail => detail.LineTotal);
        TotalAmount = GeneralDiscountPercent > 0
            ? decimal.Round(subtotal * (1m - GeneralDiscountPercent / 100m), 2, MidpointRounding.AwayFromZero)
            : decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal NormalizePercent(decimal value)
    {
        if (value < 0 || value > 100)
        {
            throw new ArgumentException("Discount percent must be between 0 and 100.", nameof(value));
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter QuoteTests`
Expected: PASS (7/7).

- [ ] **Step 7: Commit**

```bash
git add eiti.Domain/Quotes eiti.Tests/QuoteTests.cs
git commit -m "feat(quotes): add Quote aggregate, QuoteDetail and domain tests"
```

---

### Task 2: Infrastructure — EF configuration, DbSets, migration

**Files:**
- Create: `eiti.Infrastructure/Persistence/Configurations/QuoteConfiguration.cs`
- Create: `eiti.Infrastructure/Persistence/Configurations/QuoteDetailConfiguration.cs`
- Modify: `eiti.Infrastructure/Persistence/ApplicationDbContext.cs`

**Interfaces:**
- Consumes: `Quote`, `QuoteDetail`, `QuoteId`, `QuoteStatus` from Task 1.
- Produces: `ApplicationDbContext.Quotes` (`DbSet<Quote>`), `ApplicationDbContext.QuoteDetails` (`DbSet<QuoteDetail>`), migration `AddQuotes` that creates `Quotes` and `QuoteDetails` tables.

- [ ] **Step 1: Add EF configuration for `Quote`**

```csharp
// eiti.Infrastructure/Persistence/Configurations/QuoteConfiguration.cs
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("Quotes");

        builder.HasKey(quote => quote.Id);

        builder.Property(quote => quote.Id)
            .HasConversion(id => id.Value, value => new QuoteId(value))
            .IsRequired();

        builder.Property(quote => quote.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .IsRequired();

        builder.Property(quote => quote.BranchId)
            .HasConversion(id => id.Value, value => new BranchId(value))
            .IsRequired();

        builder.Property(quote => quote.CustomerId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new CustomerId(value.Value) : null)
            .IsRequired(false);

        builder.Property(quote => quote.ProspectName).HasMaxLength(200).IsRequired(false);
        builder.Property(quote => quote.ProspectContact).HasMaxLength(200).IsRequired(false);
        builder.Property(quote => quote.GeneralDiscountPercent).HasColumnType("decimal(5,2)").HasDefaultValue(0m).IsRequired();
        builder.Property(quote => quote.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(quote => quote.ExpiresAt).IsRequired();
        builder.Property(quote => quote.Status).HasColumnName("IdQuoteStatus").HasConversion<int>().IsRequired();
        builder.Property(quote => quote.ConvertedSaleId).IsRequired(false);
        builder.Property(quote => quote.Code).HasMaxLength(20).IsRequired(false);
        builder.Property(quote => quote.CreatedByUserId).IsRequired();
        builder.Property(quote => quote.CreatedAt).IsRequired();

        builder.HasIndex(quote => new { quote.CompanyId, quote.CreatedAt });
        builder.HasIndex(quote => quote.CustomerId);
        builder.HasIndex(quote => quote.BranchId);

        builder.HasOne<Company>().WithMany().HasForeignKey(quote => quote.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(quote => quote.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(quote => quote.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(quote => quote.Details)
            .WithOne()
            .HasForeignKey(detail => detail.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(quote => quote.Details)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

- [ ] **Step 2: Add EF configuration for `QuoteDetail`**

```csharp
// eiti.Infrastructure/Persistence/Configurations/QuoteDetailConfiguration.cs
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class QuoteDetailConfiguration : IEntityTypeConfiguration<QuoteDetail>
{
    public void Configure(EntityTypeBuilder<QuoteDetail> builder)
    {
        builder.ToTable("QuoteDetails");

        builder.HasKey(detail => new { detail.QuoteId, detail.ProductId });

        builder.Property(detail => detail.QuoteId)
            .HasConversion(id => id.Value, value => new eiti.Domain.Quotes.QuoteId(value))
            .IsRequired();

        builder.Property(detail => detail.ProductId)
            .HasConversion(id => id.Value, value => new ProductId(value))
            .IsRequired();

        builder.Property(detail => detail.Quantity).IsRequired();
        builder.Property(detail => detail.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(detail => detail.DiscountPercent).HasColumnType("decimal(5,2)").HasDefaultValue(0m).IsRequired();
        builder.Property(detail => detail.LineTotal).HasColumnType("decimal(18,2)").IsRequired();

        builder.HasIndex(detail => detail.ProductId);
    }
}
```

Note: the composite key `(QuoteId, ProductId)` mirrors the fact a quote can't have two lines for the same product (same assumption `CreateCcSaleHandler` makes when it groups details by `ProductId` before building `SaleDetail`s — see Task 6).

- [ ] **Step 3: Register DbSets in `ApplicationDbContext`**

Open `eiti.Infrastructure/Persistence/ApplicationDbContext.cs`, find the line:

```csharp
    public DbSet<Sale> Sales { get; set; }
```

and add immediately after the last `Sale*` DbSet (after `SaleTransportAssignment`):

```csharp
    public DbSet<eiti.Domain.Quotes.Quote> Quotes => Set<eiti.Domain.Quotes.Quote>();
    public DbSet<eiti.Domain.Quotes.QuoteDetail> QuoteDetails => Set<eiti.Domain.Quotes.QuoteDetail>();
```

- [ ] **Step 4: Build to confirm configuration compiles**

Run: `dotnet build eiti.Infrastructure/eiti.Infrastructure.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Generate the EF migration**

Run (from repo root, `eiti.Infrastructure` as target — required when the Api process may hold locked DLLs):
```bash
dotnet ef migrations add AddQuotes --project eiti.Infrastructure --startup-project eiti.Infrastructure --output-dir Migrations
```
Expected: new files `eiti.Infrastructure/Migrations/<timestamp>_AddQuotes.cs`, `.Designer.cs`, and an updated `ApplicationDbContextModelSnapshot.cs` — creating `Quotes` and `QuoteDetails` tables with the FKs/indexes above.

- [ ] **Step 6: Apply the migration locally to confirm it runs**

Run:
```bash
dotnet ef database update --project eiti.Infrastructure --startup-project eiti.Infrastructure
```
Expected: `AddQuotes` applied with no errors (the API's `Database.Migrate()` will apply it again automatically in Railway on next deploy — this local run is only to catch mistakes now).

- [ ] **Step 7: Commit**

```bash
git add eiti.Infrastructure/Persistence/Configurations/QuoteConfiguration.cs eiti.Infrastructure/Persistence/Configurations/QuoteDetailConfiguration.cs eiti.Infrastructure/Persistence/ApplicationDbContext.cs eiti.Infrastructure/Migrations
git commit -m "feat(quotes): EF configuration, DbSets and AddQuotes migration"
```

---

### Task 3: Repository — `IQuoteRepository` + `QuoteRepository`

**Files:**
- Create: `eiti.Application/Abstractions/Repositories/IQuoteRepository.cs`
- Create: `eiti.Infrastructure/Persistence/Repositories/QuoteRepository.cs`

**Interfaces:**
- Consumes: `Quote`, `QuoteId`, `QuoteStatus` (Task 1); `ApplicationDbContext.Quotes`/`.QuoteDetails` (Task 2).
- Produces: `IQuoteRepository.AddAsync(Quote, CancellationToken)`, `.GetByIdAsync(QuoteId, CompanyId, CancellationToken)`, `.ListAsync(CompanyId companyId, QuoteStatus? status, DateTime? dateFrom, DateTime? dateTo, Guid? customerId, CancellationToken)`.

- [ ] **Step 1: Define the repository interface**

```csharp
// eiti.Application/Abstractions/Repositories/IQuoteRepository.cs
using eiti.Domain.Companies;
using eiti.Domain.Quotes;

namespace eiti.Application.Abstractions.Repositories;

public interface IQuoteRepository
{
    Task AddAsync(Quote quote, CancellationToken cancellationToken = default);

    Task<Quote?> GetByIdAsync(
        QuoteId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Quote>> ListAsync(
        CompanyId companyId,
        QuoteStatus? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        Guid? customerId,
        CancellationToken cancellationToken = default);

    Task<int> CountByBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Implement `QuoteRepository`**

```csharp
// eiti.Infrastructure/Persistence/Repositories/QuoteRepository.cs
using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Quotes;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class QuoteRepository : IQuoteRepository
{
    private readonly ApplicationDbContext _context;

    public QuoteRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Quote quote, CancellationToken cancellationToken = default)
    {
        await _context.Quotes.AddAsync(quote, cancellationToken);
    }

    public async Task<Quote?> GetByIdAsync(
        QuoteId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Quotes
            .Include(quote => quote.Details)
            .FirstOrDefaultAsync(quote => quote.Id == id && quote.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<Quote>> ListAsync(
        CompanyId companyId,
        QuoteStatus? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        Guid? customerId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Quotes
            .Include(quote => quote.Details)
            .AsNoTracking()
            .Where(quote => quote.CompanyId == companyId);

        if (status.HasValue)
        {
            query = query.Where(quote => quote.Status == status.Value);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(quote => quote.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(quote => quote.CreatedAt <= dateTo.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(quote => quote.CustomerId != null && quote.CustomerId.Value == customerId.Value);
        }

        return await query
            .OrderByDescending(quote => quote.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.Quotes.CountAsync(quote => quote.BranchId.Value == branchId, cancellationToken);
    }
}
```

- [ ] **Step 3: Register the repository in DI**

Find where `ISaleRepository`/`SaleRepository` is registered (grep `AddScoped<ISaleRepository` in `eiti.Infrastructure` or `eiti.Api` DI extensions) and add the sibling line immediately after it:

```csharp
services.AddScoped<IQuoteRepository, QuoteRepository>();
```

- [ ] **Step 4: Build to confirm**

Run: `dotnet build eiti.Infrastructure/eiti.Infrastructure.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add eiti.Application/Abstractions/Repositories/IQuoteRepository.cs eiti.Infrastructure/Persistence/Repositories/QuoteRepository.cs
git commit -m "feat(quotes): add IQuoteRepository and EF implementation"
```

---

### Task 4: Permissions — `quotes.access`, `quotes.create`, `quotes.convert`

**Files:**
- Modify: `eiti.Application/Common/Authorization/PermissionCodes.cs`
- Modify: `eiti.Application/Common/Authorization/PermissionCatalog.cs`
- Modify: `eiti.Application/Common/Authorization/RoleCatalog.cs`

**Interfaces:**
- Produces: `PermissionCodes.QuotesAccess = "quotes.access"`, `PermissionCodes.QuotesCreate = "quotes.create"`, `PermissionCodes.QuotesConvert = "quotes.convert"` — consumed by every Quotes command/query (Tasks 5–9) and by the frontend guard (Task 13).

- [ ] **Step 1: Add the constants**

In `eiti.Application/Common/Authorization/PermissionCodes.cs`, after the `SalesPay` line, add:

```csharp
    public const string QuotesAccess = "quotes.access";
    public const string QuotesCreate = "quotes.create";
    public const string QuotesConvert = "quotes.convert";
```

- [ ] **Step 2: Add to the allowlist**

In `eiti.Application/Common/Authorization/PermissionCatalog.cs`, inside the `All` set, after `PermissionCodes.SalesPay,`, add:

```csharp
        PermissionCodes.QuotesAccess,
        PermissionCodes.QuotesCreate,
        PermissionCodes.QuotesConvert,
```

- [ ] **Step 3: Assign to roles**

In `eiti.Application/Common/Authorization/RoleCatalog.cs`, for the `Owner` role definition (and any other role that already has `SalesCreate`, e.g. a seller/vendedor role), add the three new permissions right after its `PermissionCodes.SalesCreate,` line:

```csharp
                PermissionCodes.QuotesAccess,
                PermissionCodes.QuotesCreate,
                PermissionCodes.QuotesConvert,
```

Repeat for every role block in `RoleCatalog.All` that currently contains `PermissionCodes.SalesCreate` (there were 3 occurrences found — Owner and two seller-type roles).

- [ ] **Step 4: Build to confirm**

Run: `dotnet build eiti.Application/eiti.Application.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add eiti.Application/Common/Authorization/PermissionCodes.cs eiti.Application/Common/Authorization/PermissionCatalog.cs eiti.Application/Common/Authorization/RoleCatalog.cs
git commit -m "feat(quotes): add quotes.access/create/convert permission codes"
```

---

### Task 5: `CreateQuote` command

**Files:**
- Create: `eiti.Application/Features/Quotes/Commands/CreateQuote/CreateQuoteCommand.cs`
- Create: `eiti.Application/Features/Quotes/Commands/CreateQuote/CreateQuoteErrors.cs`
- Create: `eiti.Application/Features/Quotes/Commands/CreateQuote/CreateQuoteResponse.cs`
- Create: `eiti.Application/Features/Quotes/Commands/CreateQuote/CreateQuoteValidator.cs`
- Create: `eiti.Application/Features/Quotes/Commands/CreateQuote/CreateQuoteHandler.cs`
- Test: `eiti.Tests/CreateQuoteHandlerTests.cs`

**Interfaces:**
- Consumes: `IQuoteRepository` (Task 3), `IBranchRepository.GetByIdAsync(BranchId, CompanyId, ct)`, `ICustomerRepository.GetByIdAsync(CustomerId, CompanyId, ct)`, `IProductRepository.GetByIdAsync(ProductId, CompanyId, ct)` (all pre-existing), `ICurrentUserService.EnsureAuthenticatedWithContext()`, `IUnitOfWork.SaveChangesAsync`.
- Produces: `CreateQuoteCommand`, `CreateQuoteResponse` — consumed by the controller (Task 10) and the frontend `CreateQuoteRequest`/`QuoteResponse` (Task 12).

- [ ] **Step 1: Define the command + item request + response**

```csharp
// eiti.Application/Features/Quotes/Commands/CreateQuote/CreateQuoteCommand.cs
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Quotes.Commands.CreateQuote;

public sealed record CreateQuoteDetailItemRequest(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent = 0);

public sealed record CreateQuoteCommand(
    Guid BranchId,
    Guid? CustomerId,
    string? ProspectName,
    string? ProspectContact,
    IReadOnlyList<CreateQuoteDetailItemRequest> Details,
    decimal GeneralDiscountPercent,
    DateTime ExpiresAt
) : IRequest<Result<CreateQuoteResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.QuotesCreate];
}
```

```csharp
// eiti.Application/Features/Quotes/Commands/CreateQuote/CreateQuoteErrors.cs
using eiti.Application.Common;

namespace eiti.Application.Features.Quotes.Commands.CreateQuote;

public static class CreateQuoteErrors
{
    public static readonly Error BranchNotFound = Error.NotFound(
        "Quotes.Create.BranchNotFound",
        "The requested branch was not found.");

    public static readonly Error CustomerNotFound = Error.NotFound(
        "Quotes.Create.CustomerNotFound",
        "The selected customer was not found.");

    public static readonly Error ProductNotFound = Error.NotFound(
        "Quotes.Create.ProductNotFound",
        "One of the requested products was not found.");

    public static readonly Error InvalidCustomerOrProspect = Error.Validation(
        "Quotes.Create.InvalidCustomerOrProspect",
        "A quote must have exactly one of an existing customer or a prospect name.");
}
```

```csharp
// eiti.Application/Features/Quotes/Commands/CreateQuote/CreateQuoteResponse.cs
namespace eiti.Application.Features.Quotes.Commands.CreateQuote;

public sealed record CreateQuoteResponse(
    Guid Id,
    string? Code,
    Guid BranchId,
    Guid? CustomerId,
    string? CustomerFullName,
    string? ProspectName,
    string? ProspectContact,
    decimal GeneralDiscountPercent,
    decimal TotalAmount,
    DateTime ExpiresAt,
    int IdQuoteStatus,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<CreateQuoteDetailItemResponse> Details);

public sealed record CreateQuoteDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal LineTotal);
```

- [ ] **Step 2: Define the validator**

```csharp
// eiti.Application/Features/Quotes/Commands/CreateQuote/CreateQuoteValidator.cs
using FluentValidation;

namespace eiti.Application.Features.Quotes.Commands.CreateQuote;

public sealed class CreateQuoteValidator : AbstractValidator<CreateQuoteCommand>
{
    public CreateQuoteValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch id is required.");

        RuleFor(x => x)
            .Must(x => x.CustomerId.HasValue != !string.IsNullOrWhiteSpace(x.ProspectName))
            .WithMessage("Provide either an existing CustomerId or a ProspectName, not both or neither.");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("At least one quote detail is required.");

        RuleForEach(x => x.Details)
            .ChildRules(detail =>
            {
                detail.RuleFor(x => x.ProductId)
                    .NotEmpty().WithMessage("Product id is required.");

                detail.RuleFor(x => x.Quantity)
                    .GreaterThan(0).WithMessage("Quantity must be greater than zero.");

                detail.RuleFor(x => x.UnitPrice)
                    .GreaterThanOrEqualTo(0m).WithMessage("Unit price cannot be negative.");
            });

        RuleFor(x => x.GeneralDiscountPercent)
            .InclusiveBetween(0m, 100m).WithMessage("General discount percent must be between 0 and 100.");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTime.UtcNow).WithMessage("ExpiresAt must be in the future.");
    }
}
```

- [ ] **Step 3: Implement the handler**

```csharp
// eiti.Application/Features/Quotes/Commands/CreateQuote/CreateQuoteHandler.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Branches;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using MediatR;

namespace eiti.Application.Features.Quotes.Commands.CreateQuote;

public sealed class CreateQuoteHandler : IRequestHandler<CreateQuoteCommand, Result<CreateQuoteResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateQuoteHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IQuoteRepository quoteRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateQuoteResponse>> Handle(CreateQuoteCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<CreateQuoteResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!.Value;

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), companyId, cancellationToken);
        if (branch is null)
        {
            return Result<CreateQuoteResponse>.Failure(CreateQuoteErrors.BranchNotFound);
        }

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _customerRepository.GetByIdAsync(new CustomerId(request.CustomerId.Value), companyId, cancellationToken);
            if (customer is null)
            {
                return Result<CreateQuoteResponse>.Failure(CreateQuoteErrors.CustomerNotFound);
            }
        }
        else if (string.IsNullOrWhiteSpace(request.ProspectName))
        {
            return Result<CreateQuoteResponse>.Failure(CreateQuoteErrors.InvalidCustomerOrProspect);
        }

        var productMap = new Dictionary<Guid, Product>();
        var quoteDetails = new List<QuoteDetail>();

        foreach (var detail in request.Details)
        {
            var product = await _productRepository.GetByIdAsync(new ProductId(detail.ProductId), companyId, cancellationToken);
            if (product is null)
            {
                return Result<CreateQuoteResponse>.Failure(CreateQuoteErrors.ProductNotFound);
            }

            productMap[product.Id.Value] = product;
            quoteDetails.Add(QuoteDetail.Create(product.Id, detail.Quantity, detail.UnitPrice, detail.DiscountPercent));
        }

        var branchQuoteCount = await _quoteRepository.CountByBranchAsync(branch.Id.Value, cancellationToken);
        var codePrefix = !string.IsNullOrWhiteSpace(branch.Code)
            ? branch.Code.ToUpper()
            : branch.Name.ToUpper()[..Math.Min(3, branch.Name.Length)];
        var quoteCode = $"PRES-{codePrefix}-{(branchQuoteCount + 1).ToString().PadLeft(3, '0')}";

        Quote quote;
        try
        {
            quote = Quote.Create(
                companyId,
                branch.Id,
                customer?.Id,
                request.ProspectName,
                request.ProspectContact,
                quoteDetails,
                request.GeneralDiscountPercent,
                request.ExpiresAt,
                userId,
                quoteCode);
        }
        catch (ArgumentException ex)
        {
            return Result<CreateQuoteResponse>.Failure(Error.Validation("Quotes.Create.InvalidInput", ex.Message));
        }

        await _quoteRepository.AddAsync(quote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateQuoteResponse>.Success(new CreateQuoteResponse(
            quote.Id.Value,
            quote.Code,
            quote.BranchId.Value,
            quote.CustomerId?.Value,
            customer?.FullName,
            quote.ProspectName,
            quote.ProspectContact,
            quote.GeneralDiscountPercent,
            quote.TotalAmount,
            quote.ExpiresAt,
            (int)quote.Status,
            quote.Status.ToString(),
            quote.CreatedAt,
            quote.Details.Select(detail => new CreateQuoteDetailItemResponse(
                detail.ProductId.Value,
                productMap[detail.ProductId.Value].Name,
                productMap[detail.ProductId.Value].Brand,
                detail.Quantity,
                detail.UnitPrice,
                detail.DiscountPercent,
                detail.LineTotal)).ToList()));
    }
}
```

- [ ] **Step 4: Write the failing handler test**

```csharp
// eiti.Tests/CreateQuoteHandlerTests.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Quotes.Commands.CreateQuote;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CreateQuoteHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreateQuote_ForProspect_WhenNoCustomerId()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);
        currentUserService.SetupGet(s => s.UserId).Returns(UserId.New());

        var branchRepository = new Mock<IBranchRepository>();
        branchRepository
            .Setup(r => r.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByIdAsync(product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var quoteRepository = new Mock<IQuoteRepository>();
        Quote? persistedQuote = null;
        quoteRepository
            .Setup(r => r.AddAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()))
            .Callback<Quote, CancellationToken>((quote, _) => persistedQuote = quote)
            .Returns(Task.CompletedTask);
        quoteRepository
            .Setup(r => r.CountByBranchAsync(branch.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new CreateQuoteHandler(
            currentUserService.Object,
            branchRepository.Object,
            new Mock<ICustomerRepository>().Object,
            productRepository.Object,
            quoteRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreateQuoteCommand(
                branch.Id.Value,
                CustomerId: null,
                ProspectName: "Juan Perez",
                ProspectContact: "1122334455",
                Details: [new CreateQuoteDetailItemRequest(product.Id.Value, 2, 150m)],
                GeneralDiscountPercent: 0,
                ExpiresAt: DateTime.UtcNow.AddDays(7)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(300m);
        result.Value.ProspectName.Should().Be("Juan Perez");
        persistedQuote.Should().NotBeNull();
        persistedQuote!.Status.Should().Be(QuoteStatus.Pending);
    }
}
```

- [ ] **Step 5: Run the test to verify it fails**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter CreateQuoteHandlerTests`
Expected: build FAILS — `CreateQuoteHandler` doesn't exist yet (if run before Step 3) or PASS immediately (if run after — in that case just confirm PASS here).

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter CreateQuoteHandlerTests`
Expected: PASS (1/1).

- [ ] **Step 7: Build the whole Application project with dependencies**

Run: `dotnet build eiti.Application/eiti.Application.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add eiti.Application/Features/Quotes/Commands/CreateQuote eiti.Tests/CreateQuoteHandlerTests.cs
git commit -m "feat(quotes): add CreateQuote command, validator and handler"
```

---

### Task 6: `CancelQuote` command

**Files:**
- Create: `eiti.Application/Features/Quotes/Commands/CancelQuote/CancelQuoteCommand.cs`
- Create: `eiti.Application/Features/Quotes/Commands/CancelQuote/CancelQuoteErrors.cs`
- Create: `eiti.Application/Features/Quotes/Commands/CancelQuote/CancelQuoteHandler.cs`

**Interfaces:**
- Consumes: `IQuoteRepository.GetByIdAsync` (Task 3), `Quote.Cancel()` (Task 1).
- Produces: `CancelQuoteCommand(Guid QuoteId)` → `Result` — consumed by the controller (Task 10).

- [ ] **Step 1: Define command and errors**

```csharp
// eiti.Application/Features/Quotes/Commands/CancelQuote/CancelQuoteCommand.cs
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Quotes.Commands.CancelQuote;

public sealed record CancelQuoteCommand(Guid QuoteId) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.QuotesAccess];
}
```

```csharp
// eiti.Application/Features/Quotes/Commands/CancelQuote/CancelQuoteErrors.cs
using eiti.Application.Common;

namespace eiti.Application.Features.Quotes.Commands.CancelQuote;

public static class CancelQuoteErrors
{
    public static readonly Error QuoteNotFound = Error.NotFound(
        "Quotes.Cancel.QuoteNotFound",
        "The requested quote was not found.");
}
```

- [ ] **Step 2: Implement the handler**

```csharp
// eiti.Application/Features/Quotes/Commands/CancelQuote/CancelQuoteHandler.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Quotes;
using MediatR;

namespace eiti.Application.Features.Quotes.Commands.CancelQuote;

public sealed class CancelQuoteHandler : IRequestHandler<CancelQuoteCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelQuoteHandler(
        ICurrentUserService currentUserService,
        IQuoteRepository quoteRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelQuoteCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return authCheck;

        var quote = await _quoteRepository.GetByIdAsync(
            new QuoteId(request.QuoteId), _currentUserService.CompanyId!, cancellationToken);
        if (quote is null)
        {
            return Result.Failure(CancelQuoteErrors.QuoteNotFound);
        }

        try
        {
            quote.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Conflict("Quotes.Cancel.InvalidState", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 3: Build to confirm**

Run: `dotnet build eiti.Application/eiti.Application.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add eiti.Application/Features/Quotes/Commands/CancelQuote
git commit -m "feat(quotes): add CancelQuote command and handler"
```

---

### Task 7: `ListQuotes` and `GetQuoteById` queries

**Files:**
- Create: `eiti.Application/Features/Quotes/Queries/ListQuotes/ListQuotesQuery.cs`
- Create: `eiti.Application/Features/Quotes/Queries/ListQuotes/QuoteListItemResponse.cs`
- Create: `eiti.Application/Features/Quotes/Queries/ListQuotes/ListQuotesHandler.cs`
- Create: `eiti.Application/Features/Quotes/Queries/GetQuoteById/GetQuoteByIdQuery.cs`
- Create: `eiti.Application/Features/Quotes/Queries/GetQuoteById/QuoteDetailResponse.cs`
- Create: `eiti.Application/Features/Quotes/Queries/GetQuoteById/GetQuoteByIdHandler.cs`
- Create: `eiti.Application/Features/Quotes/Queries/GetQuoteById/GetQuoteByIdErrors.cs`

**Interfaces:**
- Consumes: `IQuoteRepository.ListAsync`/`.GetByIdAsync` (Task 3), `IProductRepository.GetByIdsAsync` (existing, same shape used in `ListSalesHandler`), `ICustomerRepository` (existing).
- Produces: `QuoteListItemResponse` (with a derived `"Expired"` display status), `QuoteDetailResponse` — both consumed by the controller (Task 10) and the frontend `quote.models.ts` (Task 12).

- [ ] **Step 1: `ListQuotesQuery` + response**

```csharp
// eiti.Application/Features/Quotes/Queries/ListQuotes/ListQuotesQuery.cs
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Quotes;
using MediatR;

namespace eiti.Application.Features.Quotes.Queries.ListQuotes;

public sealed record ListQuotesQuery(
    QuoteStatus? Status,
    DateTime? DateFrom,
    DateTime? DateTo,
    Guid? CustomerId
) : IRequest<Result<IReadOnlyList<QuoteListItemResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.QuotesAccess];
}
```

```csharp
// eiti.Application/Features/Quotes/Queries/ListQuotes/QuoteListItemResponse.cs
namespace eiti.Application.Features.Quotes.Queries.ListQuotes;

public sealed record QuoteListItemResponse(
    Guid Id,
    string? Code,
    Guid BranchId,
    Guid? CustomerId,
    string? CustomerFullName,
    string? ProspectName,
    decimal TotalAmount,
    DateTime ExpiresAt,
    int IdQuoteStatus,
    string Status,
    bool IsExpired,
    Guid? ConvertedSaleId,
    DateTime CreatedAt);
```

- [ ] **Step 2: `ListQuotesHandler`**

```csharp
// eiti.Application/Features/Quotes/Queries/ListQuotes/ListQuotesHandler.cs
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Quotes.Queries.ListQuotes;

public sealed class ListQuotesHandler : IRequestHandler<ListQuotesQuery, Result<IReadOnlyList<QuoteListItemResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuoteRepository _quoteRepository;
    private readonly ICustomerRepository _customerRepository;

    public ListQuotesHandler(
        ICurrentUserService currentUserService,
        IQuoteRepository quoteRepository,
        ICustomerRepository customerRepository)
    {
        _currentUserService = currentUserService;
        _quoteRepository = quoteRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<IReadOnlyList<QuoteListItemResponse>>> Handle(
        ListQuotesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<QuoteListItemResponse>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var quotes = await _quoteRepository.ListAsync(
            companyId, request.Status, request.DateFrom, request.DateTo, request.CustomerId, cancellationToken);

        if (!_currentUserService.CanViewAllBranches)
        {
            var allowed = _currentUserService.AllowedBranchIds;
            quotes = quotes.Where(quote => allowed.Contains(quote.BranchId.Value)).ToList();
        }

        var customerIds = quotes
            .Where(quote => quote.CustomerId is not null)
            .Select(quote => quote.CustomerId!.Value)
            .Distinct()
            .ToList();

        var customerMap = new Dictionary<Guid, string>();
        foreach (var customerId in customerIds)
        {
            var customer = await _customerRepository.GetByIdAsync(new CustomerId(customerId), companyId, cancellationToken);
            if (customer is not null)
            {
                customerMap[customerId] = customer.FullName;
            }
        }

        var now = DateTime.UtcNow;
        return Result<IReadOnlyList<QuoteListItemResponse>>.Success(
            quotes.Select(quote => new QuoteListItemResponse(
                quote.Id.Value,
                quote.Code,
                quote.BranchId.Value,
                quote.CustomerId?.Value,
                quote.CustomerId is not null && customerMap.TryGetValue(quote.CustomerId.Value, out var name) ? name : null,
                quote.ProspectName,
                quote.TotalAmount,
                quote.ExpiresAt,
                (int)quote.Status,
                quote.Status.ToString(),
                quote.IsExpired(now),
                quote.ConvertedSaleId,
                quote.CreatedAt)).ToList());
    }
}
```

- [ ] **Step 3: `GetQuoteByIdQuery` + response + errors**

```csharp
// eiti.Application/Features/Quotes/Queries/GetQuoteById/GetQuoteByIdQuery.cs
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Quotes.Queries.GetQuoteById;

public sealed record GetQuoteByIdQuery(Guid QuoteId)
    : IRequest<Result<QuoteDetailResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.QuotesAccess];
}
```

```csharp
// eiti.Application/Features/Quotes/Queries/GetQuoteById/QuoteDetailResponse.cs
namespace eiti.Application.Features.Quotes.Queries.GetQuoteById;

public sealed record QuoteDetailResponse(
    Guid Id,
    string? Code,
    Guid BranchId,
    string BranchName,
    Guid? CustomerId,
    string? CustomerFullName,
    string? ProspectName,
    string? ProspectContact,
    decimal GeneralDiscountPercent,
    decimal TotalAmount,
    DateTime ExpiresAt,
    int IdQuoteStatus,
    string Status,
    bool IsExpired,
    Guid? ConvertedSaleId,
    DateTime CreatedAt,
    IReadOnlyList<QuoteDetailItemResponse> Details);

public sealed record QuoteDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal LineTotal);
```

```csharp
// eiti.Application/Features/Quotes/Queries/GetQuoteById/GetQuoteByIdErrors.cs
using eiti.Application.Common;

namespace eiti.Application.Features.Quotes.Queries.GetQuoteById;

public static class GetQuoteByIdErrors
{
    public static readonly Error QuoteNotFound = Error.NotFound(
        "Quotes.GetById.QuoteNotFound",
        "The requested quote was not found.");
}
```

- [ ] **Step 4: `GetQuoteByIdHandler`**

```csharp
// eiti.Application/Features/Quotes/Queries/GetQuoteById/GetQuoteByIdHandler.cs
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using MediatR;

namespace eiti.Application.Features.Quotes.Queries.GetQuoteById;

public sealed class GetQuoteByIdHandler : IRequestHandler<GetQuoteByIdQuery, Result<QuoteDetailResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuoteRepository _quoteRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;

    public GetQuoteByIdHandler(
        ICurrentUserService currentUserService,
        IQuoteRepository quoteRepository,
        IBranchRepository branchRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository)
    {
        _currentUserService = currentUserService;
        _quoteRepository = quoteRepository;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<QuoteDetailResponse>> Handle(GetQuoteByIdQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<QuoteDetailResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var quote = await _quoteRepository.GetByIdAsync(new QuoteId(request.QuoteId), companyId, cancellationToken);
        if (quote is null)
        {
            return Result<QuoteDetailResponse>.Failure(GetQuoteByIdErrors.QuoteNotFound);
        }

        var branch = await _branchRepository.GetByIdAsync(quote.BranchId, companyId, cancellationToken);

        string? customerFullName = null;
        if (quote.CustomerId is not null)
        {
            var customer = await _customerRepository.GetByIdAsync(quote.CustomerId, companyId, cancellationToken);
            customerFullName = customer?.FullName;
        }

        var productIds = quote.Details.Select(detail => detail.ProductId).Distinct().ToList();
        var products = await _productRepository.GetByIdsAsync(productIds, companyId, cancellationToken);
        var productMap = products.ToDictionary(product => product.Id.Value, product => product);

        var now = DateTime.UtcNow;
        return Result<QuoteDetailResponse>.Success(new QuoteDetailResponse(
            quote.Id.Value,
            quote.Code,
            quote.BranchId.Value,
            branch?.Name ?? string.Empty,
            quote.CustomerId?.Value,
            customerFullName,
            quote.ProspectName,
            quote.ProspectContact,
            quote.GeneralDiscountPercent,
            quote.TotalAmount,
            quote.ExpiresAt,
            (int)quote.Status,
            quote.Status.ToString(),
            quote.IsExpired(now),
            quote.ConvertedSaleId,
            quote.CreatedAt,
            quote.Details.Select(detail => new QuoteDetailItemResponse(
                detail.ProductId.Value,
                productMap.TryGetValue(detail.ProductId.Value, out var product) ? product.Name : "Deleted product",
                productMap.TryGetValue(detail.ProductId.Value, out var product2) ? product2.Brand : "Unknown",
                detail.Quantity,
                detail.UnitPrice,
                detail.DiscountPercent,
                detail.LineTotal)).ToList()));
    }
}
```

- [ ] **Step 5: Build to confirm**

Run: `dotnet build eiti.Application/eiti.Application.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add eiti.Application/Features/Quotes/Queries
git commit -m "feat(quotes): add ListQuotes and GetQuoteById queries"
```

---

### Task 8: `ConvertQuoteToSale` command

**Files:**
- Create: `eiti.Application/Features/Quotes/Commands/ConvertQuoteToSale/ConvertQuoteToSaleCommand.cs`
- Create: `eiti.Application/Features/Quotes/Commands/ConvertQuoteToSale/ConvertQuoteToSaleErrors.cs`
- Create: `eiti.Application/Features/Quotes/Commands/ConvertQuoteToSale/ConvertQuoteToSaleHandler.cs`
- Test: `eiti.Tests/ConvertQuoteToSaleHandlerTests.cs`

**Interfaces:**
- Consumes: `CreateCcSaleCommand`/`CreateCcSaleResponse` (existing, `eiti.Application.Features.Sales.Commands.CreateCcSale`), `IQuoteRepository.GetByIdAsync` (Task 3), `Quote.MarkConverted(Guid, DateTime)`/`.IsExpired` (Task 1), `IMediator.Send`.
- Produces: `ConvertQuoteToSaleCommand` → `Result<CreateCcSaleResponse>` — consumed by the controller (Task 10) and the frontend `ConvertQuoteRequest` (Task 12).

- [ ] **Step 1: Define the command**

The request shape mirrors `CreateCcSaleCommand` exactly (same editable fields), plus the `QuoteId` being converted:

```csharp
// eiti.Application/Features/Quotes/Commands/ConvertQuoteToSale/ConvertQuoteToSaleCommand.cs
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Sales.Commands.CreateCcSale;
using eiti.Application.Features.Sales.Commands.CreateSale;
using MediatR;

namespace eiti.Application.Features.Quotes.Commands.ConvertQuoteToSale;

public sealed record ConvertQuoteToSaleCommand(
    Guid QuoteId,
    Guid BranchId,
    Guid CustomerId,
    IReadOnlyList<CreateSaleDetailItemRequest> Details,
    IReadOnlyList<CreateSaleTradeInItemRequest>? TradeIns = null,
    decimal GeneralDiscountPercent = 0,
    decimal? ManualOverridePrice = null
) : IRequest<Result<CreateCcSaleResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.QuotesConvert];
}
```

- [ ] **Step 2: Define errors**

```csharp
// eiti.Application/Features/Quotes/Commands/ConvertQuoteToSale/ConvertQuoteToSaleErrors.cs
using eiti.Application.Common;

namespace eiti.Application.Features.Quotes.Commands.ConvertQuoteToSale;

public static class ConvertQuoteToSaleErrors
{
    public static readonly Error QuoteNotFound = Error.NotFound(
        "Quotes.Convert.QuoteNotFound",
        "The requested quote was not found.");

    public static readonly Error NotPending = Error.Conflict(
        "Quotes.Convert.NotPending",
        "Only a pending quote can be converted into a sale.");

    public static readonly Error Expired = Error.Conflict(
        "Quotes.Convert.Expired",
        "This quote has expired and can no longer be converted directly.");
}
```

- [ ] **Step 3: Implement the handler — dispatches `CreateCcSaleCommand` internally**

```csharp
// eiti.Application/Features/Quotes/Commands/ConvertQuoteToSale/ConvertQuoteToSaleHandler.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Sales.Commands.CreateCcSale;
using eiti.Domain.Quotes;
using MediatR;

namespace eiti.Application.Features.Quotes.Commands.ConvertQuoteToSale;

public sealed class ConvertQuoteToSaleHandler : IRequestHandler<ConvertQuoteToSaleCommand, Result<CreateCcSaleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuoteRepository _quoteRepository;
    private readonly ISender _sender;
    private readonly IUnitOfWork _unitOfWork;

    public ConvertQuoteToSaleHandler(
        ICurrentUserService currentUserService,
        IQuoteRepository quoteRepository,
        ISender sender,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _quoteRepository = quoteRepository;
        _sender = sender;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateCcSaleResponse>> Handle(
        ConvertQuoteToSaleCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CreateCcSaleResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var quote = await _quoteRepository.GetByIdAsync(new QuoteId(request.QuoteId), companyId, cancellationToken);
        if (quote is null)
        {
            return Result<CreateCcSaleResponse>.Failure(ConvertQuoteToSaleErrors.QuoteNotFound);
        }

        if (quote.Status != QuoteStatus.Pending)
        {
            return Result<CreateCcSaleResponse>.Failure(ConvertQuoteToSaleErrors.NotPending);
        }

        var now = DateTime.UtcNow;
        if (quote.IsExpired(now))
        {
            return Result<CreateCcSaleResponse>.Failure(ConvertQuoteToSaleErrors.Expired);
        }

        var createSaleResult = await _sender.Send(
            new CreateCcSaleCommand(
                request.BranchId,
                request.CustomerId,
                request.Details,
                request.TradeIns,
                request.GeneralDiscountPercent,
                request.ManualOverridePrice),
            cancellationToken);

        if (createSaleResult.IsFailure)
        {
            return Result<CreateCcSaleResponse>.Failure(createSaleResult.Error);
        }

        quote.MarkConverted(createSaleResult.Value.Id, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return createSaleResult;
    }
}
```

Note: `CreateCcSaleHandler` already calls `_unitOfWork.SaveChangesAsync` internally (it persists the `Sale` before this handler runs `quote.MarkConverted`), so the second `SaveChangesAsync` call here only persists the `Quote` status change — no double-write of the sale itself.

- [ ] **Step 4: Write the failing handler test — happy path + expired rejection**

```csharp
// eiti.Tests/ConvertQuoteToSaleHandlerTests.cs
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Sales.Commands.CreateCcSale;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Application.Features.Quotes.Commands.ConvertQuoteToSale;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using FluentAssertions;
using MediatR;
using Moq;

namespace eiti.Tests;

public sealed class ConvertQuoteToSaleHandlerTests
{
    private static Quote BuildQuote(DateTime expiresAt, CompanyId companyId, BranchId branchId)
    {
        return Quote.Create(
            companyId, branchId, CustomerId.New(), null, null,
            new[] { QuoteDetail.Create(ProductId.New(), 1, 100m) },
            0, expiresAt, Guid.NewGuid());
    }

    [Fact]
    public async Task Handle_ShouldMarkConverted_WhenPendingAndNotExpired()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var quote = BuildQuote(DateTime.UtcNow.AddDays(7), companyId, branchId);
        var saleId = Guid.NewGuid();

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);

        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(r => r.GetByIdAsync(quote.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<CreateCcSaleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateCcSaleResponse>.Success(new CreateCcSaleResponse(
                saleId, "SC-001", branchId.Value, Guid.NewGuid(), "Juan Perez",
                1, "OnHold", 0, 100m, 100m, null, true, DateTime.UtcNow,
                0, 0, [], 0, 100m, [])));

        var handler = new ConvertQuoteToSaleHandler(
            currentUserService.Object,
            quoteRepository.Object,
            sender.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new ConvertQuoteToSaleCommand(
                quote.Id.Value, branchId.Value, Guid.NewGuid(),
                [new CreateSaleDetailItemRequest(Guid.NewGuid(), 1, null, 0)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(saleId);
        quote.Status.Should().Be(QuoteStatus.Converted);
        quote.ConvertedSaleId.Should().Be(saleId);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenQuoteExpired()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var quote = BuildQuote(DateTime.UtcNow.AddSeconds(-1), companyId, branchId);

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);

        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(r => r.GetByIdAsync(quote.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var handler = new ConvertQuoteToSaleHandler(
            currentUserService.Object,
            quoteRepository.Object,
            new Mock<ISender>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new ConvertQuoteToSaleCommand(
                quote.Id.Value, branchId.Value, Guid.NewGuid(),
                [new CreateSaleDetailItemRequest(Guid.NewGuid(), 1, null, 0)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Quotes.Convert.Expired");
        quote.Status.Should().Be(QuoteStatus.Pending);
    }
}
```

Check the actual `CreateSaleDetailItemRequest` record shape before Step 4 (grep `record CreateSaleDetailItemRequest` in `eiti.Application/Features/Sales/Commands/CreateSale/CreateSaleCommand.cs`) and adjust the constructor args above to match its real parameter order/names if they differ.

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test eiti.Tests/eiti.Tests.csproj --filter ConvertQuoteToSaleHandlerTests`
Expected: PASS (2/2).

- [ ] **Step 6: Build to confirm**

Run: `dotnet build eiti.Application/eiti.Application.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add eiti.Application/Features/Quotes/Commands/ConvertQuoteToSale eiti.Tests/ConvertQuoteToSaleHandlerTests.cs
git commit -m "feat(quotes): add ConvertQuoteToSale command reusing CreateCcSale"
```

---

### Task 9: `QuotesController`

**Files:**
- Create: `eiti.Api/Controllers/QuotesController.cs`

**Interfaces:**
- Consumes: `CreateQuoteCommand`, `CancelQuoteCommand`, `ListQuotesQuery`, `GetQuoteByIdQuery`, `ConvertQuoteToSaleCommand` (Tasks 5–8), `ResultExtensions.ToActionResult()` (existing, same as `SalesController`).
- Produces: `POST /api/quotes`, `GET /api/quotes`, `GET /api/quotes/{id}`, `POST /api/quotes/{id}/cancel`, `POST /api/quotes/{id}/convert` — consumed by the frontend `quote.service.ts` (Task 12).

- [ ] **Step 1: Implement the controller**

```csharp
// eiti.Api/Controllers/QuotesController.cs
using eiti.Api.Extensions;
using eiti.Application.Features.Quotes.Commands.CancelQuote;
using eiti.Application.Features.Quotes.Commands.ConvertQuoteToSale;
using eiti.Application.Features.Quotes.Commands.CreateQuote;
using eiti.Application.Features.Quotes.Queries.GetQuoteById;
using eiti.Application.Features.Quotes.Queries.ListQuotes;
using eiti.Domain.Quotes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class QuotesController : ControllerBase
{
    private readonly ISender _sender;

    public QuotesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateQuote(
        [FromBody] CreateQuoteCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> ListQuotes(
        [FromQuery] int? idQuoteStatus,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? customerId,
        CancellationToken cancellationToken)
    {
        QuoteStatus? status = idQuoteStatus.HasValue ? (QuoteStatus)idQuoteStatus.Value : null;
        var result = await _sender.Send(new ListQuotesQuery(status, dateFrom, dateTo, customerId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetQuoteById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetQuoteByIdQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelQuote(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelQuoteCommand(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/convert")]
    public async Task<IActionResult> ConvertQuoteToSale(
        Guid id,
        [FromBody] ConvertQuoteToSaleRequestBody body,
        CancellationToken cancellationToken)
    {
        var command = new ConvertQuoteToSaleCommand(
            id, body.BranchId, body.CustomerId, body.Details, body.TradeIns,
            body.GeneralDiscountPercent, body.ManualOverridePrice);
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record ConvertQuoteToSaleRequestBody(
    Guid BranchId,
    Guid CustomerId,
    IReadOnlyList<eiti.Application.Features.Sales.Commands.CreateSale.CreateSaleDetailItemRequest> Details,
    IReadOnlyList<eiti.Application.Features.Sales.Commands.CreateSale.CreateSaleTradeInItemRequest>? TradeIns,
    decimal GeneralDiscountPercent,
    decimal? ManualOverridePrice);
```

The route body is a small record instead of binding `ConvertQuoteToSaleCommand` directly, because the command's first parameter is `QuoteId` (which comes from the route, not the body) — same reasoning as any command whose id is split between route and body elsewhere in this controller family.

- [ ] **Step 2: Build the Api project**

Run: `dotnet build eiti.Api/eiti.Api.csproj`
Expected: Build succeeded, 0 errors. If it fails with MSB3027 (locked DLL), stop the running API process first, then retry.

- [ ] **Step 3: Commit**

```bash
git add eiti.Api/Controllers/QuotesController.cs
git commit -m "feat(quotes): add QuotesController with create/list/get/cancel/convert endpoints"
```

---

### Task 10: Frontend — models, service, permissions

**Files:**
- Create: `src/app/core/models/quote.models.ts`
- Create: `src/app/core/services/quote.service.ts`
- Modify: `src/app/core/models/permission.models.ts`

**Interfaces:**
- Consumes: `environment.apiUrl` (existing), `CreateSaleDetailRequest`, `SaleTradeInRequest` types (existing, from `sale.models.ts`).
- Produces: `QuoteResponse`, `QuoteListItem`, `QuoteDetailItem`, `CreateQuoteRequest`, `ConvertQuoteRequest`, `QuoteStatusCode` — consumed by every component in Tasks 11–13.

- [ ] **Step 1: Add permission codes**

In `src/app/core/models/permission.models.ts`, after the `salesPay: 'sales.pay',` line, add:

```typescript
    quotesAccess: 'quotes.access',
    quotesCreate: 'quotes.create',
    quotesConvert: 'quotes.convert',
```

And in the `PermissionCatalog` array (near the sales entries), add:

```typescript
    { code: PermissionCodes.quotesAccess, label: 'Presupuestos: acceso', description: 'Permite ingresar al modulo de presupuestos.' },
    { code: PermissionCodes.quotesCreate, label: 'Presupuestos: crear', description: 'Permite crear presupuestos nuevos.' },
    { code: PermissionCodes.quotesConvert, label: 'Presupuestos: convertir', description: 'Permite convertir un presupuesto en una venta de cuenta corriente.' },
```

- [ ] **Step 2: Create `quote.models.ts`**

```typescript
// src/app/core/models/quote.models.ts
import { CreateSaleDetailRequest, SaleTradeInRequest, CreateCcSaleResponse } from './sale.models';

export type QuoteStatusCode = 1 | 2 | 3; // Pending | Converted | Cancelled

export interface QuoteDetailItem {
    productId: string;
    productName: string;
    productBrand: string;
    quantity: number;
    unitPrice: number;
    discountPercent: number;
    lineTotal: number;
}

export interface QuoteListItem {
    id: string;
    code?: string | null;
    branchId: string;
    customerId?: string | null;
    customerFullName?: string | null;
    prospectName?: string | null;
    totalAmount: number;
    expiresAt: string;
    idQuoteStatus: QuoteStatusCode;
    status: string;
    isExpired: boolean;
    convertedSaleId?: string | null;
    createdAt: string;
}

export interface QuoteDetailResponse {
    id: string;
    code?: string | null;
    branchId: string;
    branchName: string;
    customerId?: string | null;
    customerFullName?: string | null;
    prospectName?: string | null;
    prospectContact?: string | null;
    generalDiscountPercent: number;
    totalAmount: number;
    expiresAt: string;
    idQuoteStatus: QuoteStatusCode;
    status: string;
    isExpired: boolean;
    convertedSaleId?: string | null;
    createdAt: string;
    details: QuoteDetailItem[];
}

export interface CreateQuoteDetailRequest {
    productId: string;
    quantity: number;
    unitPrice: number;
    discountPercent?: number;
}

export interface CreateQuoteRequest {
    branchId: string;
    customerId?: string | null;
    prospectName?: string | null;
    prospectContact?: string | null;
    details: CreateQuoteDetailRequest[];
    generalDiscountPercent: number;
    expiresAt: string;
}

export interface ConvertQuoteRequest {
    branchId: string;
    customerId: string;
    details: CreateSaleDetailRequest[];
    tradeIns?: SaleTradeInRequest[];
    generalDiscountPercent?: number;
    manualOverridePrice?: number | null;
}

export type ConvertQuoteResponse = CreateCcSaleResponse;

export interface ListQuotesFilters {
    idQuoteStatus?: QuoteStatusCode;
    dateFrom?: string;
    dateTo?: string;
    customerId?: string;
}
```

Check `sale.models.ts` for the exact exported name of the trade-in request interface (`SaleTradeInRequest` is assumed above — confirm with `grep "TradeInRequest" src/app/core/models/sale.models.ts` and adjust the import if the real name differs).

- [ ] **Step 3: Create `quote.service.ts`**

```typescript
// src/app/core/services/quote.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
    QuoteListItem,
    QuoteDetailResponse,
    CreateQuoteRequest,
    ConvertQuoteRequest,
    ConvertQuoteResponse,
    ListQuotesFilters
} from '../models/quote.models';

@Injectable({ providedIn: 'root' })
export class QuoteService {
    private readonly base = `${environment.apiUrl}/quotes`;

    constructor(private readonly http: HttpClient) {}

    listQuotes(filters: ListQuotesFilters = {}): Observable<QuoteListItem[]> {
        const params: Record<string, string> = {};
        if (filters.idQuoteStatus) { params['idQuoteStatus'] = String(filters.idQuoteStatus); }
        if (filters.dateFrom) { params['dateFrom'] = filters.dateFrom; }
        if (filters.dateTo) { params['dateTo'] = filters.dateTo; }
        if (filters.customerId) { params['customerId'] = filters.customerId; }
        return this.http.get<QuoteListItem[]>(this.base, { params });
    }

    getQuoteById(id: string): Observable<QuoteDetailResponse> {
        return this.http.get<QuoteDetailResponse>(`${this.base}/${id}`);
    }

    createQuote(request: CreateQuoteRequest): Observable<QuoteDetailResponse> {
        return this.http.post<QuoteDetailResponse>(this.base, request);
    }

    cancelQuote(id: string): Observable<void> {
        return this.http.post<void>(`${this.base}/${id}/cancel`, {});
    }

    convertQuote(id: string, request: ConvertQuoteRequest): Observable<ConvertQuoteResponse> {
        return this.http.post<ConvertQuoteResponse>(`${this.base}/${id}/convert`, request);
    }
}
```

- [ ] **Step 4: Build to confirm**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: build succeeds (these are new, unreferenced files at this point — a strict-mode compile error here means a type mismatch against `sale.models.ts` that must be fixed before continuing).

- [ ] **Step 5: Commit**

```bash
git add src/app/core/models/quote.models.ts src/app/core/services/quote.service.ts src/app/core/models/permission.models.ts
git commit -m "feat(quotes): add quote models, QuoteService and permission codes"
```

---

### Task 11: Frontend — `quotes-list` component

**Files:**
- Create: `src/app/features/quotes/quotes-list/quotes-list.component.ts`
- Create: `src/app/features/quotes/quotes-list/quotes-list.component.html`
- Create: `src/app/features/quotes/quotes-list/quotes-list.component.css`

**Interfaces:**
- Consumes: `QuoteService.listQuotes`/`.cancelQuote` (Task 10), `AuthService.hasPermission` (existing), `ToastService` (existing).
- Produces: `QuotesListComponent` (selector `app-quotes-list`) — the component `app.routes.ts` will lazy-load in Task 14; emits `openDetail` events consumed by whatever parent wires the detail modal (Task 13 wires it as a sibling in the same page — see Task 14).

- [ ] **Step 1: Implement the component class**

```typescript
// src/app/features/quotes/quotes-list/quotes-list.component.ts
import { Component, OnInit, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { QuoteService } from '../../../core/services/quote.service';
import { QuoteListItem, QuoteStatusCode } from '../../../core/models/quote.models';
import { ToastService } from '../../../shared/services/toast.service';
import { extractApiError } from '../../../shared/utils/api-error.util';

@Component({
    selector: 'app-quotes-list',
    standalone: true,
    imports: [CommonModule, FormsModule],
    changeDetection: ChangeDetectionStrategy.OnPush,
    templateUrl: './quotes-list.component.html',
    styleUrls: ['./quotes-list.component.css']
})
export class QuotesListComponent implements OnInit {
    quotes: QuoteListItem[] = [];
    loading = false;
    statusFilter: QuoteStatusCode | '' = '';

    @Output() openDetail = new EventEmitter<string>();
    @Output() convertRequested = new EventEmitter<QuoteListItem>();

    constructor(
        private readonly quoteService: QuoteService,
        private readonly toast: ToastService
    ) {}

    ngOnInit(): void {
        this.reload();
    }

    reload(): void {
        this.loading = true;
        this.quoteService.listQuotes(this.statusFilter ? { idQuoteStatus: this.statusFilter } : {}).subscribe({
            next: quotes => {
                this.quotes = quotes;
                this.loading = false;
            },
            error: err => {
                this.loading = false;
                this.toast.error(extractApiError(err, 'No se pudieron cargar los presupuestos'));
            }
        });
    }

    onStatusFilterChange(value: string): void {
        this.statusFilter = value ? (Number(value) as QuoteStatusCode) : '';
        this.reload();
    }

    statusLabel(quote: QuoteListItem): string {
        if (quote.idQuoteStatus === 1 && quote.isExpired) { return 'Vencido'; }
        if (quote.idQuoteStatus === 1) { return 'Pendiente'; }
        if (quote.idQuoteStatus === 2) { return 'Convertido'; }
        return 'Cancelado';
    }

    statusClass(quote: QuoteListItem): string {
        if (quote.idQuoteStatus === 1 && quote.isExpired) { return 'chip--quote-expired'; }
        if (quote.idQuoteStatus === 1) { return 'chip--quote-pending'; }
        if (quote.idQuoteStatus === 2) { return 'chip--quote-converted'; }
        return 'chip--quote-cancelled';
    }

    canConvert(quote: QuoteListItem): boolean {
        return quote.idQuoteStatus === 1 && !quote.isExpired;
    }

    cancelQuote(quote: QuoteListItem): void {
        this.quoteService.cancelQuote(quote.id).subscribe({
            next: () => {
                this.toast.success('Presupuesto cancelado');
                this.reload();
            },
            error: err => this.toast.error(extractApiError(err, 'No se pudo cancelar el presupuesto'))
        });
    }
}
```

- [ ] **Step 2: Implement the template**

```html
<!-- src/app/features/quotes/quotes-list/quotes-list.component.html -->
<div class="quotes-list">
  <div class="quotes-list__filters">
    <select (change)="onStatusFilterChange($any($event.target).value)">
      <option value="">Todos</option>
      <option value="1">Pendiente</option>
      <option value="2">Convertido</option>
      <option value="3">Cancelado</option>
    </select>
  </div>

  <p *ngIf="loading">Cargando presupuestos...</p>

  <table class="quotes-list__table" *ngIf="!loading">
    <thead>
      <tr>
        <th>Codigo</th>
        <th>Cliente</th>
        <th>Total</th>
        <th>Vence</th>
        <th>Estado</th>
        <th></th>
      </tr>
    </thead>
    <tbody>
      <tr *ngFor="let quote of quotes">
        <td>{{ quote.code }}</td>
        <td>{{ quote.customerFullName ?? quote.prospectName }}</td>
        <td>{{ quote.totalAmount | number: '1.2-2' }}</td>
        <td>{{ quote.expiresAt | date: 'dd/MM/yyyy' }}</td>
        <td><span class="chip" [ngClass]="statusClass(quote)">{{ statusLabel(quote) }}</span></td>
        <td class="quotes-list__actions">
          <button type="button" (click)="openDetail.emit(quote.id)">Ver</button>
          <button type="button" *ngIf="canConvert(quote)" (click)="convertRequested.emit(quote)">Convertir</button>
          <button type="button" *ngIf="quote.idQuoteStatus === 1" (click)="cancelQuote(quote)">Cancelar</button>
        </td>
      </tr>
    </tbody>
  </table>
</div>
```

- [ ] **Step 3: Minimal CSS**

```css
/* src/app/features/quotes/quotes-list/quotes-list.component.css */
.quotes-list__table { width: 100%; border-collapse: collapse; }
.quotes-list__table th, .quotes-list__table td { padding: 0.5rem; border-bottom: 1px solid var(--border-2); text-align: left; }
.quotes-list__actions { display: flex; gap: 0.5rem; }
.chip { padding: 0.15rem 0.6rem; border-radius: 999px; font-size: 0.8rem; }
.chip--quote-pending { background: var(--bg-panel); color: var(--text); }
.chip--quote-expired { background: #5a1f1f; color: #ffb4b4; }
.chip--quote-converted { background: #1f4d2b; color: #b8f0c4; }
.chip--quote-cancelled { background: var(--bg-panel); color: var(--text-dim); }
```

- [ ] **Step 4: Build to confirm**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/app/features/quotes/quotes-list
git commit -m "feat(quotes): add quotes-list component"
```

---

### Task 12: Frontend — `quote-form` component (create)

**Files:**
- Create: `src/app/features/quotes/quote-form/quote-form.component.ts`
- Create: `src/app/features/quotes/quote-form/quote-form.component.html`
- Create: `src/app/features/quotes/quote-form/quote-form.component.css`

**Interfaces:**
- Consumes: `QuoteService.createQuote` (Task 10), `BranchService.listBranches`, `CustomerService.searchCustomers`, `StockService.listBranchStock` (all existing, same as `sales-cc.component.ts`), `ProductPickerModalComponent` (existing, shared).
- Produces: `QuoteFormComponent` (selector `app-quote-form`), emits `created: EventEmitter<void>` consumed by the container page (Task 14) to trigger `QuotesListComponent.reload()`.

- [ ] **Step 1: Implement the component class**

This reuses the exact `DraftItem`/stock-loading/product-picker pattern already proven in `sales-cc.component.ts` (Task 10 read that file in full during planning), but with no stock reservation and an extra prospect/customer toggle:

```typescript
// src/app/features/quotes/quote-form/quote-form.component.ts
import { Component, OnInit, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BranchService } from '../../../core/services/branch.service';
import { CustomerService } from '../../../core/services/customer.service';
import { StockService } from '../../../core/services/stock.service';
import { QuoteService } from '../../../core/services/quote.service';
import { ToastService } from '../../../shared/services/toast.service';
import { extractApiError } from '../../../shared/utils/api-error.util';
import { BranchResponse } from '../../../core/models/branch.models';
import { CustomerSearchItem } from '../../../core/models/customer.models';
import { BranchProductStockResponse } from '../../../core/models/stock.models';
import { CreateQuoteRequest } from '../../../core/models/quote.models';
import { ProductPickerModalComponent } from '../../../shared/components/product-picker-modal/product-picker-modal.component';
import { ProductPickerRow, ProductPickerSelection, toProductPickerRow } from '../../../shared/components/product-picker-modal/product-picker-modal.models';

interface QuoteDraftItem {
    stock: BranchProductStockResponse;
    quantity: number;
    discountPercent: number;
    unitPrice: number;
    total: number;
}

@Component({
    selector: 'app-quote-form',
    standalone: true,
    imports: [CommonModule, FormsModule, ProductPickerModalComponent],
    templateUrl: './quote-form.component.html',
    styleUrls: ['./quote-form.component.css']
})
export class QuoteFormComponent implements OnInit {
    branches: BranchResponse[] = [];
    selectedBranchId = '';

    customerMode: 'existing' | 'prospect' = 'existing';
    customerQuery = '';
    searchResults: CustomerSearchItem[] = [];
    selectedCustomer: CustomerSearchItem | null = null;
    prospectName = '';
    prospectContact = '';

    generalDiscountPercent = 0;
    expiresAt = this.defaultExpiresAt();

    draftItems: QuoteDraftItem[] = [];
    stockItems: BranchProductStockResponse[] = [];
    stockByProductId = new Map<string, BranchProductStockResponse>();
    productModalOpen = false;
    pickerRows: ProductPickerRow[] = [];

    saving = false;

    @Output() created = new EventEmitter<void>();

    constructor(
        private readonly branchService: BranchService,
        private readonly customerService: CustomerService,
        private readonly stockService: StockService,
        private readonly quoteService: QuoteService,
        private readonly toast: ToastService
    ) {}

    ngOnInit(): void {
        this.branchService.listBranches().subscribe({
            next: branches => {
                this.branches = branches;
                if (branches.length > 0) { this.selectedBranchId = branches[0].id; }
            },
            error: () => this.toast.error('No se pudieron cargar las sucursales')
        });
    }

    private defaultExpiresAt(): string {
        const date = new Date();
        date.setDate(date.getDate() + 7);
        return date.toISOString().slice(0, 10);
    }

    get total(): number {
        const subtotal = this.draftItems.reduce((sum, item) => sum + item.total, 0);
        if (this.generalDiscountPercent > 0) {
            return Math.round(subtotal * (1 - this.generalDiscountPercent / 100) * 100) / 100;
        }
        return subtotal;
    }

    get canSubmit(): boolean {
        const hasClient = this.customerMode === 'existing' ? !!this.selectedCustomer : this.prospectName.trim().length > 0;
        return !this.saving && hasClient && this.draftItems.length > 0 && !!this.selectedBranchId && !!this.expiresAt;
    }

    searchCustomers(): void {
        const query = this.customerQuery.trim();
        if (!query) { this.toast.error('Ingresa un termino de busqueda'); return; }
        this.customerService.searchCustomers(query).subscribe({
            next: results => { this.searchResults = results; },
            error: () => this.toast.error('No se pudo buscar clientes')
        });
    }

    selectCustomer(customer: CustomerSearchItem): void {
        this.selectedCustomer = customer;
        this.searchResults = [];
        this.customerQuery = '';
    }

    openProductModal(): void {
        if (!this.selectedBranchId) { this.toast.error('Selecciona una sucursal primero'); return; }
        if (this.stockItems.length === 0) {
            this.stockService.listBranchStock(this.selectedBranchId).subscribe({
                next: items => {
                    this.stockItems = items;
                    this.stockByProductId.clear();
                    for (const item of items) { this.stockByProductId.set(item.productId, item); }
                    this.buildPickerRows();
                    this.productModalOpen = true;
                },
                error: () => this.toast.error('No se pudo cargar el stock de la sucursal')
            });
        } else {
            this.buildPickerRows();
            this.productModalOpen = true;
        }
    }

    private buildPickerRows(): void {
        this.pickerRows = this.stockItems.map(stock =>
            toProductPickerRow(
                { id: stock.productId, code: stock.code, sku: stock.sku, brand: stock.brand, name: stock.name },
                999
            )
        );
    }

    closeProductModal(): void {
        this.productModalOpen = false;
        this.pickerRows = [];
    }

    onPickerConfirm(selection: ProductPickerSelection[]): void {
        for (const { id, quantity } of selection) {
            const stock = this.stockByProductId.get(id);
            if (!stock || quantity <= 0) { continue; }
            const existing = this.draftItems.find(item => item.stock.productId === id);
            if (existing) {
                existing.quantity += Math.floor(quantity);
                this.recalcItem(existing);
            } else {
                const item: QuoteDraftItem = {
                    stock,
                    quantity: Math.floor(quantity),
                    discountPercent: 0,
                    unitPrice: stock.publicPrice ?? stock.price ?? 0,
                    total: 0
                };
                this.recalcItem(item);
                this.draftItems.unshift(item);
            }
        }
        this.closeProductModal();
    }

    private recalcItem(item: QuoteDraftItem): void {
        item.total = Math.round(item.quantity * item.unitPrice * (1 - item.discountPercent / 100) * 100) / 100;
    }

    setItemPrice(productId: string, value: number | null): void {
        const item = this.draftItems.find(i => i.stock.productId === productId);
        if (!item) { return; }
        item.unitPrice = value != null && Number.isFinite(value) && value >= 0 ? Math.round(value * 100) / 100 : item.unitPrice;
        this.recalcItem(item);
    }

    removeItem(productId: string): void {
        this.draftItems = this.draftItems.filter(item => item.stock.productId !== productId);
    }

    submit(): void {
        if (!this.canSubmit) { return; }
        this.saving = true;

        const request: CreateQuoteRequest = {
            branchId: this.selectedBranchId,
            customerId: this.customerMode === 'existing' ? this.selectedCustomer!.id : null,
            prospectName: this.customerMode === 'prospect' ? this.prospectName.trim() : null,
            prospectContact: this.customerMode === 'prospect' ? this.prospectContact.trim() || null : null,
            details: this.draftItems.map(item => ({
                productId: item.stock.productId,
                quantity: item.quantity,
                unitPrice: item.unitPrice,
                discountPercent: item.discountPercent || undefined
            })),
            generalDiscountPercent: this.generalDiscountPercent || 0,
            expiresAt: new Date(this.expiresAt).toISOString()
        };

        this.quoteService.createQuote(request).subscribe({
            next: () => {
                this.saving = false;
                this.toast.success('Presupuesto creado');
                this.resetForm();
                this.created.emit();
            },
            error: err => {
                this.saving = false;
                this.toast.error(extractApiError(err, 'No se pudo crear el presupuesto'));
            }
        });
    }

    private resetForm(): void {
        this.selectedCustomer = null;
        this.prospectName = '';
        this.prospectContact = '';
        this.draftItems = [];
        this.generalDiscountPercent = 0;
        this.expiresAt = this.defaultExpiresAt();
    }
}
```

- [ ] **Step 2: Implement the template**

```html
<!-- src/app/features/quotes/quote-form/quote-form.component.html -->
<div class="quote-form">
  <label>Sucursal
    <select [(ngModel)]="selectedBranchId">
      <option *ngFor="let branch of branches" [value]="branch.id">{{ branch.name }}</option>
    </select>
  </label>

  <div class="quote-form__customer-toggle">
    <label><input type="radio" name="customerMode" value="existing" [(ngModel)]="customerMode"> Cliente existente</label>
    <label><input type="radio" name="customerMode" value="prospect" [(ngModel)]="customerMode"> Cliente sin cargar</label>
  </div>

  <ng-container *ngIf="customerMode === 'existing'">
    <div *ngIf="!selectedCustomer">
      <input type="text" [(ngModel)]="customerQuery" placeholder="Buscar cliente" (keyup.enter)="searchCustomers()">
      <button type="button" (click)="searchCustomers()">Buscar</button>
      <ul>
        <li *ngFor="let customer of searchResults" (click)="selectCustomer(customer)">{{ customer.fullName }}</li>
      </ul>
    </div>
    <div *ngIf="selectedCustomer">
      <span>{{ selectedCustomer.fullName }}</span>
      <button type="button" (click)="selectedCustomer = null">Cambiar</button>
    </div>
  </ng-container>

  <ng-container *ngIf="customerMode === 'prospect'">
    <label>Nombre <input type="text" [(ngModel)]="prospectName"></label>
    <label>Contacto <input type="text" [(ngModel)]="prospectContact"></label>
  </ng-container>

  <label>Valido hasta <input type="date" [(ngModel)]="expiresAt"></label>

  <button type="button" (click)="openProductModal()">Agregar productos</button>

  <table>
    <tbody>
      <tr *ngFor="let item of draftItems">
        <td>{{ item.stock.brand }} {{ item.stock.name }}</td>
        <td>{{ item.quantity }}</td>
        <td><input type="number" [value]="item.unitPrice" (change)="setItemPrice(item.stock.productId, $any($event.target).valueAsNumber)"></td>
        <td>{{ item.total | number: '1.2-2' }}</td>
        <td><button type="button" (click)="removeItem(item.stock.productId)">Quitar</button></td>
      </tr>
    </tbody>
  </table>

  <label>Descuento general (%) <input type="number" [(ngModel)]="generalDiscountPercent"></label>
  <div>Total: {{ total | number: '1.2-2' }}</div>

  <button type="button" [disabled]="!canSubmit" (click)="submit()">Crear presupuesto</button>

  <app-product-picker-modal
    *ngIf="productModalOpen"
    [rows]="pickerRows"
    (confirm)="onPickerConfirm($event)"
    (close)="closeProductModal()">
  </app-product-picker-modal>
</div>
```

Check `ProductPickerModalComponent`'s actual `@Input`/`@Output` names (`rows`/`confirm`/`close` are assumed from the `sales-cc.component.html` usage pattern — confirm with `grep "app-product-picker-modal" -A5 src/app/features/sales/sales-cc/sales-cc.component.html`) and adjust the bindings above if they differ.

- [ ] **Step 3: Minimal CSS**

```css
/* src/app/features/quotes/quote-form/quote-form.component.css */
.quote-form { display: flex; flex-direction: column; gap: 0.75rem; max-width: 720px; }
.quote-form__customer-toggle { display: flex; gap: 1rem; }
```

- [ ] **Step 4: Build to confirm**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/app/features/quotes/quote-form
git commit -m "feat(quotes): add quote-form component for creating quotes"
```

---

### Task 13: Frontend — `quote-detail-modal` + PDF export

**Files:**
- Create: `src/app/features/quotes/quote-detail-modal/quote-detail-modal.component.ts`
- Create: `src/app/features/quotes/quote-detail-modal/quote-detail-modal.component.html`
- Create: `src/app/features/quotes/quote-detail-modal/quote-detail-modal.component.css`
- Create: `src/app/shared/utils/quote-pdf.util.ts`

**Interfaces:**
- Consumes: `QuoteService.getQuoteById` (Task 10), `jsPDF` (existing dependency, same one used by the sale remito PDF utility — check its exact import path with `grep -rn "from 'jspdf'" src/app/shared/utils/`).
- Produces: `QuoteDetailModalComponent` (selector `app-quote-detail-modal`, `@Input() quoteId: string`, `@Output() close`, `@Output() convert: EventEmitter<QuoteDetailResponse>`) and `generateQuotePdf(quote: QuoteDetailResponse): void` — the `convert` output is consumed by the container page (Task 14) to launch the `sales-cc` prefill flow (Task 14/15).

- [ ] **Step 1: Implement `quote-pdf.util.ts`**

```typescript
// src/app/shared/utils/quote-pdf.util.ts
import jsPDF from 'jspdf';
import { QuoteDetailResponse } from '../../core/models/quote.models';

export function generateQuotePdf(quote: QuoteDetailResponse): void {
    const doc = new jsPDF();
    let y = 15;

    doc.setFontSize(16);
    doc.text(`Presupuesto ${quote.code ?? ''}`, 14, y);
    y += 10;

    doc.setFontSize(10);
    doc.text(`Cliente: ${quote.customerFullName ?? quote.prospectName ?? '-'}`, 14, y);
    y += 6;
    doc.text(`Sucursal: ${quote.branchName}`, 14, y);
    y += 6;
    doc.text(`Fecha: ${new Date(quote.createdAt).toLocaleDateString('es-AR')}`, 14, y);
    y += 6;
    doc.setTextColor(200, 0, 0);
    doc.text(`Valido hasta: ${new Date(quote.expiresAt).toLocaleDateString('es-AR')}`, 14, y);
    doc.setTextColor(0, 0, 0);
    y += 10;

    doc.setFontSize(9);
    doc.text('Producto', 14, y);
    doc.text('Cant.', 120, y);
    doc.text('Precio unit.', 145, y);
    doc.text('Total', 180, y);
    y += 4;
    doc.line(14, y, 196, y);
    y += 6;

    for (const detail of quote.details) {
        doc.text(`${detail.productBrand} ${detail.productName}`.slice(0, 60), 14, y);
        doc.text(String(detail.quantity), 120, y);
        doc.text(detail.unitPrice.toFixed(2), 145, y);
        doc.text(detail.lineTotal.toFixed(2), 180, y);
        y += 6;
    }

    y += 4;
    doc.line(14, y, 196, y);
    y += 8;
    doc.setFontSize(12);
    doc.text(`Total: $${quote.totalAmount.toFixed(2)}`, 14, y);
    y += 12;

    doc.setFontSize(8);
    doc.text('Presupuesto - no constituye una venta.', 14, y);

    doc.save(`presupuesto-${quote.code ?? quote.id}.pdf`);
}
```

Confirm the actual `jsPDF` import statement used elsewhere in the project (`import jsPDF from 'jspdf'` vs `import { jsPDF } from 'jspdf'`) by checking the existing remito PDF utility, and match it exactly.

- [ ] **Step 2: Implement the modal component**

```typescript
// src/app/features/quotes/quote-detail-modal/quote-detail-modal.component.ts
import { Component, Input, OnChanges, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { QuoteService } from '../../../core/services/quote.service';
import { QuoteDetailResponse } from '../../../core/models/quote.models';
import { generateQuotePdf } from '../../../shared/utils/quote-pdf.util';
import { ToastService } from '../../../shared/services/toast.service';
import { extractApiError } from '../../../shared/utils/api-error.util';

@Component({
    selector: 'app-quote-detail-modal',
    standalone: true,
    imports: [CommonModule],
    templateUrl: './quote-detail-modal.component.html',
    styleUrls: ['./quote-detail-modal.component.css']
})
export class QuoteDetailModalComponent implements OnChanges {
    @Input({ required: true }) quoteId!: string;
    @Output() close = new EventEmitter<void>();
    @Output() convert = new EventEmitter<QuoteDetailResponse>();

    quote: QuoteDetailResponse | null = null;
    loading = false;

    constructor(
        private readonly quoteService: QuoteService,
        private readonly toast: ToastService
    ) {}

    ngOnChanges(): void {
        if (!this.quoteId) { return; }
        this.loading = true;
        this.quoteService.getQuoteById(this.quoteId).subscribe({
            next: quote => {
                this.quote = quote;
                this.loading = false;
            },
            error: err => {
                this.loading = false;
                this.toast.error(extractApiError(err, 'No se pudo cargar el presupuesto'));
            }
        });
    }

    get canConvert(): boolean {
        return !!this.quote && this.quote.idQuoteStatus === 1 && !this.quote.isExpired;
    }

    downloadPdf(): void {
        if (this.quote) { generateQuotePdf(this.quote); }
    }

    requestConvert(): void {
        if (this.quote && this.canConvert) { this.convert.emit(this.quote); }
    }
}
```

- [ ] **Step 3: Implement the template**

```html
<!-- src/app/features/quotes/quote-detail-modal/quote-detail-modal.component.html -->
<div class="modal-backdrop" (click)="close.emit()">
  <div class="modal" (click)="$event.stopPropagation()">
    <p *ngIf="loading">Cargando...</p>
    <ng-container *ngIf="quote as q">
      <h3>Presupuesto {{ q.code }}</h3>
      <p>Cliente: {{ q.customerFullName ?? q.prospectName }}</p>
      <p>Valido hasta: {{ q.expiresAt | date: 'dd/MM/yyyy' }}</p>
      <table>
        <tbody>
          <tr *ngFor="let detail of q.details">
            <td>{{ detail.productBrand }} {{ detail.productName }}</td>
            <td>{{ detail.quantity }}</td>
            <td>{{ detail.unitPrice | number: '1.2-2' }}</td>
            <td>{{ detail.lineTotal | number: '1.2-2' }}</td>
          </tr>
        </tbody>
      </table>
      <p>Total: {{ q.totalAmount | number: '1.2-2' }}</p>
      <div class="modal__actions">
        <button type="button" (click)="downloadPdf()">Descargar PDF</button>
        <button type="button" *ngIf="canConvert" (click)="requestConvert()">Convertir a venta</button>
        <button type="button" (click)="close.emit()">Cerrar</button>
      </div>
    </ng-container>
  </div>
</div>
```

- [ ] **Step 4: Minimal CSS**

```css
/* src/app/features/quotes/quote-detail-modal/quote-detail-modal.component.css */
.modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; }
.modal { background: var(--bg-panel); padding: 1.5rem; border-radius: 8px; max-width: 640px; width: 100%; }
.modal__actions { display: flex; gap: 0.5rem; margin-top: 1rem; }
```

- [ ] **Step 5: Build to confirm**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/app/features/quotes/quote-detail-modal src/app/shared/utils/quote-pdf.util.ts
git commit -m "feat(quotes): add quote detail modal and PDF export"
```

---

### Task 14: Frontend — `quotes.component` container, routes, navbar link

**Files:**
- Create: `src/app/features/quotes/quotes.component.ts`
- Create: `src/app/features/quotes/quotes.component.html`
- Modify: `src/app/app.routes.ts`
- Modify: `src/app/shared/components/navbar/navbar.component.html`

**Interfaces:**
- Consumes: `QuotesListComponent` (Task 11), `QuoteFormComponent` (Task 12), `QuoteDetailModalComponent` (Task 13), `PermissionCodes.quotesAccess` (Task 10).
- Produces: `/quotes` route — the `convertRequested`/`convert` events are wired here to `router.navigateByUrl('/sales-cc', { state: ... })`, which Task 15 makes `sales-cc.component.ts` consume.

- [ ] **Step 1: Implement the container component**

```typescript
// src/app/features/quotes/quotes.component.ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { QuotesListComponent } from './quotes-list/quotes-list.component';
import { QuoteFormComponent } from './quote-form/quote-form.component';
import { QuoteDetailModalComponent } from './quote-detail-modal/quote-detail-modal.component';
import { QuoteListItem, QuoteDetailResponse } from '../../core/models/quote.models';

@Component({
    selector: 'app-quotes',
    standalone: true,
    imports: [CommonModule, QuotesListComponent, QuoteFormComponent, QuoteDetailModalComponent],
    templateUrl: './quotes.component.html'
})
export class QuotesComponent {
    showForm = false;
    detailQuoteId: string | null = null;

    constructor(private readonly router: Router) {}

    onCreated(): void {
        this.showForm = false;
    }

    openDetail(id: string): void {
        this.detailQuoteId = id;
    }

    closeDetail(): void {
        this.detailQuoteId = null;
    }

    convertQuote(quote: QuoteDetailResponse | QuoteListItem): void {
        this.detailQuoteId = null;
        this.router.navigateByUrl('/sales-cc', {
            state: {
                quotePrefill: {
                    quoteId: quote.id,
                    branchId: quote.branchId,
                    customerId: 'customerId' in quote ? quote.customerId : null,
                    customerFullName: 'customerFullName' in quote ? quote.customerFullName : null,
                    prospectName: 'prospectName' in quote ? quote.prospectName : null,
                    generalDiscountPercent: 'generalDiscountPercent' in quote ? quote.generalDiscountPercent : 0,
                    details: 'details' in quote
                        ? quote.details.map(detail => ({
                            productId: detail.productId,
                            productName: detail.productName,
                            productBrand: detail.productBrand,
                            quantity: detail.quantity,
                            unitPrice: detail.unitPrice,
                            discountPercent: detail.discountPercent
                        }))
                        : []
                }
            }
        });
    }
}
```

- [ ] **Step 2: Implement the container template**

```html
<!-- src/app/features/quotes/quotes.component.html -->
<div class="quotes-page">
  <div class="quotes-page__header">
    <h2>Presupuestos</h2>
    <button type="button" (click)="showForm = !showForm">{{ showForm ? 'Cerrar' : 'Nuevo presupuesto' }}</button>
  </div>

  <app-quote-form *ngIf="showForm" (created)="onCreated()"></app-quote-form>

  <app-quotes-list
    (openDetail)="openDetail($event)"
    (convertRequested)="convertQuote($event)">
  </app-quotes-list>

  <app-quote-detail-modal
    *ngIf="detailQuoteId"
    [quoteId]="detailQuoteId"
    (close)="closeDetail()"
    (convert)="convertQuote($event)">
  </app-quote-detail-modal>
</div>
```

Note: `QuotesListComponent.reload()` only runs on init and after cancel — after `onCreated()` closes the form, the list will only refresh via `QuotesListComponent`'s own `ngOnInit` re-trigger. If it doesn't auto-refresh visually, add a `@ViewChild(QuotesListComponent) list!: QuotesListComponent;` in `QuotesComponent` and call `this.list.reload()` inside `onCreated()` — verify this manually in Step 4 below and add it if needed.

- [ ] **Step 3: Add the route**

In `src/app/app.routes.ts`, after the `banks` route block, add:

```typescript
    {
        path: 'quotes',
        canActivate: [authGuard, permissionGuard],
        data: { permission: PermissionCodes.quotesAccess },
        loadComponent: () =>
            import('./features/quotes/quotes.component').then(m => m.QuotesComponent)
    },
```

- [ ] **Step 4: Add the navbar link**

In `src/app/shared/components/navbar/navbar.component.html`, inside the sales submenu (`.sidebar__submenu` block containing `/sales-cc`), after the `/sales-cc` link's closing `</a>`, add:

```html
          <a *ngIf="auth.hasPermission(permissionCodes.quotesAccess)"
             class="sidebar__submenu-item" routerLink="/quotes" routerLinkActive="is-active">
            <svg class="sidebar__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
              <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/>
              <path d="M14 2v6h6"/>
              <path d="M9 13h6"/>
              <path d="M9 17h6"/>
            </svg>
            <span>Presupuestos</span>
          </a>
```

- [ ] **Step 5: Build and manually verify list-refresh-after-create (from Step 2's note)**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: build succeeds. Then start `ng serve`, log in, navigate to `/quotes`, create a quote, and confirm the list updates without a manual page refresh. If it doesn't, apply the `@ViewChild` fix noted in Step 2.

- [ ] **Step 6: Commit**

```bash
git add src/app/features/quotes/quotes.component.ts src/app/features/quotes/quotes.component.html src/app/app.routes.ts src/app/shared/components/navbar/navbar.component.html
git commit -m "feat(quotes): add quotes container page, route and navbar link"
```

---

### Task 15: Frontend — wire quote conversion into `sales-cc`

**Files:**
- Modify: `src/app/features/sales/sales-cc/sales-cc.component.ts`
- Modify: `src/app/features/sales/sales-cc/sales-cc.component.html`

**Interfaces:**
- Consumes: `history.state.quotePrefill` (set by Task 14's `router.navigateByUrl` call), `QuoteService.convertQuote` (Task 10).
- Produces: when `quotePrefill` is present, `submit()` calls `QuoteService.convertQuote(quoteId, ...)` instead of `SaleService.createCcSale(...)`.

- [ ] **Step 1: Read the prefill on init and prepopulate the draft**

In `sales-cc.component.ts`, add the import and a new field, then extend `ngOnInit`:

```typescript
import { Router } from '@angular/router';
import { QuoteService } from '../../../core/services/quote.service';
```

Add to the constructor:

```typescript
    private readonly router: Router,
    private readonly quoteService: QuoteService,
```

Add fields near `saving`:

```typescript
  convertingQuoteId: string | null = null;
  private pendingQuotePrefill: {
    quoteId: string;
    branchId: string;
    customerId: string | null;
    prospectName: string | null;
    generalDiscountPercent: number;
    details: { productId: string; quantity: number; unitPrice: number; discountPercent: number }[];
  } | null = null;
```

Modify `ngOnInit` to capture `history.state.quotePrefill` and apply it once branches (and, for its branch, stock) are loaded:

```typescript
  ngOnInit(): void {
    const state = this.router.getCurrentNavigation()?.extras.state ?? history.state;
    this.pendingQuotePrefill = state?.['quotePrefill'] ?? null;

    this.branchService.listBranches().subscribe({
      next: branches => {
        this.branches = branches;
        if (this.pendingQuotePrefill) {
          this.selectedBranchId = this.pendingQuotePrefill.branchId;
          this.convertingQuoteId = this.pendingQuotePrefill.quoteId;
          this.generalDiscountPercent = this.pendingQuotePrefill.generalDiscountPercent;
          this.applyQuotePrefillItems();
        } else if (branches.length > 0) {
          this.selectedBranchId = branches[0].id;
        }
      },
      error: () => this.toast.error('No se pudieron cargar las sucursales')
    });
  }

  private applyQuotePrefillItems(): void {
    if (!this.pendingQuotePrefill || !this.selectedBranchId) { return; }
    this.stockService.listBranchStock(this.selectedBranchId).subscribe({
      next: items => {
        this.stockItems = items;
        this.stockByProductId.clear();
        for (const item of items) { this.stockByProductId.set(item.productId, item); }

        for (const detail of this.pendingQuotePrefill!.details) {
          const stock = this.stockByProductId.get(detail.productId);
          if (!stock) { continue; }
          this.draftItems.push({
            stock,
            quantity: detail.quantity,
            discountPercent: detail.discountPercent,
            unitPriceOverride: detail.unitPrice,
            total: 0
          });
          this.recalcItem(this.draftItems[this.draftItems.length - 1]);
        }

        if (this.pendingQuotePrefill!.customerId) {
          this.customerService.searchCustomers(this.pendingQuotePrefill!.prospectName ?? '').subscribe();
        }
        this.toast.success(this.pendingQuotePrefill!.customerId
          ? 'Presupuesto cargado. Revisa los datos antes de confirmar.'
          : 'Presupuesto cargado. Selecciona el cliente antes de confirmar.');
      },
      error: () => this.toast.error('No se pudo cargar el stock de la sucursal')
    });
  }
```

Note on customer resolution: if `pendingQuotePrefill.customerId` is set (the quote already had a real customer), the cleanest fix is to add a lightweight `CustomerService.getById(id)` call (check if it already exists — grep `getById` in `customer.service.ts`) and call `this.selectCustomer(result)` with it instead of the placeholder `searchCustomers` call above. Use that if available; otherwise fall back to requiring the user to re-search and re-select the customer manually (acceptable for a v1, and still satisfies "prospect must resolve a customer before confirming" from the spec).

- [ ] **Step 2: Route `submit()` through `ConvertQuoteToSale` when converting**

Modify the `submit()` method's HTTP call section — replace:

```typescript
    this.saleService.createCcSale({
      branchId: this.selectedBranchId,
      customerId: this.selectedCustomer.id,
      details,
      tradeIns: this.tradeInDrafts.length
        ? this.tradeInDrafts.map(t => ({ productId: t.productId, quantity: t.quantity, amount: t.amount }))
        : undefined,
      generalDiscountPercent: this.generalDiscountPercent || undefined,
      manualOverridePrice: this.manualOverridePrice ?? undefined
    }).subscribe({
```

with:

```typescript
    const payload = {
      branchId: this.selectedBranchId,
      customerId: this.selectedCustomer.id,
      details,
      tradeIns: this.tradeInDrafts.length
        ? this.tradeInDrafts.map(t => ({ productId: t.productId, quantity: t.quantity, amount: t.amount }))
        : undefined,
      generalDiscountPercent: this.generalDiscountPercent || undefined,
      manualOverridePrice: this.manualOverridePrice ?? undefined
    };

    const request$ = this.convertingQuoteId
      ? this.quoteService.convertQuote(this.convertingQuoteId, payload)
      : this.saleService.createCcSale(payload);

    request$.subscribe({
```

And in the `next` callback, after `this.pickerRows = [];`, add:

```typescript
        this.convertingQuoteId = null;
        this.pendingQuotePrefill = null;
```

- [ ] **Step 3: Add a small banner in the template when converting a quote**

In `sales-cc.component.html`, near the top of the form, add:

```html
<div class="quote-conversion-banner" *ngIf="convertingQuoteId">
  Convirtiendo presupuesto a venta CC. Revisa cliente, productos y precios antes de confirmar.
</div>
```

- [ ] **Step 4: Build to confirm**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: build succeeds.

- [ ] **Step 5: Manual end-to-end verification**

Start the app (`ng serve` + API running locally), then:
1. Go to `/quotes`, create a presupuesto for a prospect (no customer).
2. Download the PDF, confirm it opens and shows "Presupuesto — no constituye una venta" and the expiry date.
3. Click "Convertir a venta" — confirm it navigates to `/sales-cc` with items/branch/discount pre-filled and the banner visible.
4. Select a real customer (since it was a prospect), confirm — verify the CC sale is created (`ListCcSales` shows it) and back on `/quotes` the presupuesto shows status "Convertido".
5. Create a second presupuesto, cancel it, confirm it shows "Cancelado" and has no "Convertir" button.

- [ ] **Step 6: Commit**

```bash
git add src/app/features/sales/sales-cc/sales-cc.component.ts src/app/features/sales/sales-cc/sales-cc.component.html
git commit -m "feat(quotes): wire quote-to-CC-sale conversion into sales-cc component"
```

---

## Self-Review Notes

- **Spec coverage:** Quote model/invariants → Task 1. EF persistence → Task 2. Repository → Task 3. Permissions (3 backend spots + frontend mirror) → Tasks 4, 10. Create/Cancel/List/GetById/Convert → Tasks 5–8. Controller → Task 9. Frontend models/service → Task 10. List/create/detail UI → Tasks 11–13. PDF → Task 13. Route/nav → Task 14. Editable conversion into `sales-cc` → Task 15. Domain + handler tests → Tasks 1, 5, 8.
- **Placeholder scan:** no `TBD`/`TODO` left; the two "confirm the exact shape by grepping X" notes (Task 8 Step 4, Task 12 Step 2, Task 13 Step 1) are intentional — they tell the implementer to verify an assumption against a file this plan's author read only partially, not to defer real work.
- **Type consistency:** `QuoteDetailItemResponse`/`CreateQuoteDetailItemResponse`/`QuoteDetailItem` (TS) all carry the same 7 fields (`productId`, `productName`, `productBrand`, `quantity`, `unitPrice`, `discountPercent`, `lineTotal`/`LineTotal`) end to end. `ConvertQuoteToSaleCommand` fields match `CreateCcSaleCommand` exactly (Task 8) so the internal `_sender.Send(new CreateCcSaleCommand(...))` call lines up positionally.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-16-quotes-presupuestos-implementation.md`. Two execution options:

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration
2. **Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
