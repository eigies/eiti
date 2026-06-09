using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Reports.Queries.DailySalesControl;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class DailySalesControlHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnProductsReferencesAndTradeIns_ForAllowedBranches()
    {
        var companyId = CompanyId.New();
        var allowedBranch = Branch.Create(companyId, "Centro", "CTR", null);
        var otherBranch = Branch.Create(companyId, "Norte", "NTE", null);
        var battery = Product.Create(
            companyId, "BAT-100", "BAT-100", "Moura", "M100", null, 200m, 120m, null);
        var tradeInProduct = Product.Create(
            companyId, "USADA", "USADA", "Generica", "Bateria usada", null, 0m, 0m, null, true);
        var customer = Customer.Create(companyId, "Ana", "Perez", null);

        var visibleSale = Sale.Create(
            companyId,
            allowedBranch.Id,
            customer.Id,
            false,
            SaleStatus.Paid,
            [SaleDetail.Create(battery.Id, 1, 200m)],
            [SalePayment.Create(SalePaymentMethod.Transfer, 150m, "TRX-9988")],
            [SaleTradeIn.Create(tradeInProduct.Id, 1, 50m)],
            allowOverpayment: true,
            code: "V-0001");

        var hiddenSale = Sale.Create(
            companyId,
            otherBranch.Id,
            null,
            false,
            SaleStatus.Paid,
            [SaleDetail.Create(battery.Id, 1, 200m)],
            [SalePayment.Create(SalePaymentMethod.Cash, 200m, null)],
            allowOverpayment: true,
            code: "V-0002");

        var currentUser = new Mock<ICurrentUserService>();
        var sales = new Mock<ISaleRepository>();
        var products = new Mock<IProductRepository>();
        var customers = new Mock<ICustomerRepository>();
        var branches = new Mock<IBranchRepository>();

        currentUser.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUser.SetupGet(service => service.CompanyId).Returns(companyId);
        currentUser.SetupGet(service => service.CanViewAllBranches).Returns(false);
        currentUser.SetupGet(service => service.AllowedBranchIds)
            .Returns([allowedBranch.Id.Value]);

        sales.Setup(repository => repository.ListByCompanyAsync(
                companyId,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([visibleSale, hiddenSale]);
        products.Setup(repository => repository.GetByCompanyIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([battery, tradeInProduct]);
        branches.Setup(repository => repository.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([allowedBranch, otherBranch]);
        customers.Setup(repository => repository.GetByIdAsync(customer.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        var handler = new DailySalesControlHandler(
            currentUser.Object,
            sales.Object,
            products.Object,
            customers.Object,
            branches.Object);

        var result = await handler.Handle(
            new DailySalesControlQuery(DateTime.Today, DateTime.Today, 0),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().ContainSingle();

        var row = result.Value.Rows[0];
        row.Code.Should().Be("V-0001");
        row.BranchName.Should().Be("Centro");
        row.CustomerName.Should().Be("Ana Perez");
        row.Products.Should().ContainSingle(item =>
            item.Code == "BAT-100" && item.Brand == "Moura" && item.Quantity == 1);
        row.Payments.Should().ContainSingle(item =>
            item.MethodCode == (int)SalePaymentMethod.Transfer && item.Reference == "TRX-9988");
        row.TradeIns.Should().ContainSingle(item =>
            item.Code == "USADA" && item.Amount == 50m);
        result.Value.Totals.SalesCount.Should().Be(1);
        result.Value.Totals.UnitsSold.Should().Be(1);
        result.Value.Totals.SalesWithTradeIn.Should().Be(1);
        result.Value.Totals.TotalAmount.Should().Be(200m);
    }

    [Fact]
    public async Task Handle_ShouldExcludeCancelledSales_WhenStatusIsActive()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Centro", null, null);
        var battery = Product.Create(
            companyId, "BAT-100", "BAT-100", "Moura", "M100", null, 200m, 120m, null);
        var cancelledSale = Sale.Create(
            companyId,
            branch.Id,
            null,
            false,
            SaleStatus.OnHold,
            [SaleDetail.Create(battery.Id, 1, 200m)],
            code: "V-CANCEL");
        cancelledSale.Update(
            null,
            SaleStatus.Cancel,
            false,
            [SaleDetail.Create(battery.Id, 1, 200m)]);

        var currentUser = new Mock<ICurrentUserService>();
        var sales = new Mock<ISaleRepository>();
        var products = new Mock<IProductRepository>();
        var customers = new Mock<ICustomerRepository>();
        var branches = new Mock<IBranchRepository>();

        currentUser.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUser.SetupGet(service => service.CompanyId).Returns(companyId);
        currentUser.SetupGet(service => service.CanViewAllBranches).Returns(true);
        sales.Setup(repository => repository.ListByCompanyAsync(
                companyId,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([cancelledSale]);
        products.Setup(repository => repository.GetByCompanyIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([battery]);
        branches.Setup(repository => repository.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([branch]);

        var handler = new DailySalesControlHandler(
            currentUser.Object,
            sales.Object,
            products.Object,
            customers.Object,
            branches.Object);

        var result = await handler.Handle(
            new DailySalesControlQuery(DateTime.Today, DateTime.Today, 0),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().BeEmpty();
        result.Value.Totals.SalesCount.Should().Be(0);
    }
}
