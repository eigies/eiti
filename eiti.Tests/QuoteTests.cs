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
    public void Create_ShouldSumVatOnNetPrices_WhenIncludesVat()
    {
        // Precios NETOS: 2 x 100 = 200 neto. Con IVA 21% => IVA 42, total 242.
        var quote = Quote.Create(
            CompanyId.New(), BranchId.New(), customerId: CustomerId.New(),
            prospectName: null, prospectContact: null,
            details: new[] { QuoteDetail.Create(ProductId.New(), 2, 100m) },
            generalDiscountPercent: 0,
            expiresAt: DateTime.UtcNow.AddDays(7),
            createdByUserId: Guid.NewGuid(),
            vatRate: 21m, includesVat: true);

        quote.NetAmount.Should().Be(200m);
        quote.VatAmount.Should().Be(42m);
        quote.GrandTotal.Should().Be(242m);
    }

    [Fact]
    public void VatAmount_ShouldBeZero_WhenNotIncludesVat()
    {
        var quote = Quote.Create(
            CompanyId.New(), BranchId.New(), customerId: CustomerId.New(),
            prospectName: null, prospectContact: null,
            details: new[] { QuoteDetail.Create(ProductId.New(), 2, 100m) },
            generalDiscountPercent: 0,
            expiresAt: DateTime.UtcNow.AddDays(7),
            createdByUserId: Guid.NewGuid(),
            vatRate: 21m, includesVat: false);

        quote.VatAmount.Should().Be(0m);
        quote.GrandTotal.Should().Be(200m);
    }

    [Fact]
    public void Create_ShouldThrow_WhenVatRateNotAllowed()
    {
        var act = () => Quote.Create(
            CompanyId.New(), BranchId.New(), customerId: CustomerId.New(),
            prospectName: null, prospectContact: null,
            details: new[] { SampleDetail() },
            generalDiscountPercent: 0,
            expiresAt: DateTime.UtcNow.AddDays(7),
            createdByUserId: Guid.NewGuid(),
            vatRate: 15m, includesVat: true);

        act.Should().Throw<ArgumentException>();
    }

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
