using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;

namespace eiti.Tests;

public sealed class SaleSettlementTests
{
    [Fact]
    public void MarkAsPaid_ShouldFail_WhenSettlementIsBelow()
    {
        var sale = CreateBaseSale(
            [SalePayment.Create(SalePaymentMethod.Transfer, 50m, null)],
            []);

        var act = () => sale.MarkAsPaid(null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must cover the total amount*");
    }

    [Fact]
    public void MarkAsPaid_ShouldSucceed_WhenSettlementExceedsTotal()
    {
        var sale = CreateBaseSale(
            [SalePayment.Create(SalePaymentMethod.Transfer, 150m, "TRX-002")],
            [],
            allowOverpayment: true);

        sale.MarkAsPaid(null);

        sale.SaleStatus.Should().Be(SaleStatus.Paid);
        sale.ChangeAmount.Should().Be(50m);
    }

    [Fact]
    public void MarkAsPaid_ShouldRequireCashSession_WhenCashAmountExists()
    {
        var sale = CreateBaseSale(
            [SalePayment.Create(SalePaymentMethod.Cash, 100m, null)],
            []);

        var act = () => sale.MarkAsPaid(null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cash session is required*");
    }

    [Fact]
    public void MarkAsPaid_ShouldAllowNullCashSession_WhenNoCashPayment()
    {
        var sale = CreateBaseSale(
            [SalePayment.Create(SalePaymentMethod.Transfer, 100m, "TRX-001")],
            []);

        sale.MarkAsPaid(null);

        sale.SaleStatus.Should().Be(SaleStatus.Paid);
        sale.CashSessionId.Should().BeNull();
        sale.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void Update_ShouldFail_WhenOnHoldSettlementExceedsTotal()
    {
        var sale = CreateBaseSale(
            [SalePayment.Create(SalePaymentMethod.Transfer, 100m, null)],
            []);

        var companyId = sale.CompanyId;
        var branchId = sale.BranchId;
        var product = Product.Create(companyId, "BAT-002", "BAT-002", "Contoso", "Bateria 75Ah", null, 100m, 70m, null);

        var act = () => sale.Update(
            customerId: null,
            saleStatus: SaleStatus.OnHold,
            hasDelivery: false,
            details: [SaleDetail.Create(product.Id, 1, product.Price)],
            payments: [SalePayment.Create(SalePaymentMethod.Transfer, 120m, null)],
            tradeIns: []);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot exceed the total amount*");
    }

    private static Sale CreateBaseSale(
        IReadOnlyList<SalePayment> payments,
        IReadOnlyList<SaleTradeIn> tradeIns,
        bool allowOverpayment = false)
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria 65Ah", null, 100m, 70m, null);

        return Sale.Create(
            companyId,
            branchId,
            customerId: null,
            hasDelivery: false,
            saleStatus: SaleStatus.OnHold,
            details: [SaleDetail.Create(product.Id, 1, product.Price)],
            payments: payments,
            tradeIns: tradeIns,
            allowOverpayment: allowOverpayment);
    }
}
