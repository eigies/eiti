using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Reports.Queries.WholesaleByCustomer;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

// El reporte de ventas por cliente tambien sirve para minoristas: los clientes que vuelven a comprar
// (o compran para revender) tienen que verse arriba, con su ultima compra, y no tapados por las ventas
// a consumidor final, que son la mayoria.
public sealed class SalesByCustomerReportTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly BranchId _branchId = BranchId.New();
    private readonly Product _battery;
    private readonly Customer _frequent;

    public SalesByCustomerReportTests()
    {
        _battery = Product.Create(_companyId, "BAT-001", "BAT-001", "Moura", "Bateria 12x65", null, 100000m, 70000m, null);
        _frequent = Customer.Create(_companyId, "Taller", "Ruiz", null);
    }

    [Fact]
    public async Task LastSaleAt_IsTheMostRecentSaleOfTheCustomerInThePeriod()
    {
        var older = RetailSale(_frequent.Id, 1, new DateTime(2026, 10, 3, 15, 0, 0, DateTimeKind.Utc));
        var newer = RetailSale(_frequent.Id, 1, new DateTime(2026, 10, 21, 13, 30, 0, DateTimeKind.Utc));

        var report = await Run("retail", newer, older);

        var row = report.Rows.Should().ContainSingle().Subject;
        row.Operations.Should().Be(2);
        row.LastSaleAt.Should().Be(new DateTime(2026, 10, 21, 13, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task WalkInCustomers_GoLast_EvenWhenTheyBuyTheMost()
    {
        var walkIn = RetailSale(null, 5, new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc));
        var frequent = RetailSale(_frequent.Id, 1, new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc));

        var report = await Run("retail", walkIn, frequent);

        report.Rows.Select(r => r.CustomerName).Should().Equal("Taller Ruiz", "(Consumidor final)");
        report.Rows[1].Revenue.Should().BeGreaterThan(report.Rows[0].Revenue);
    }

    private Sale RetailSale(CustomerId? customerId, int quantity, DateTime createdAt)
    {
        var sale = Sale.Create(_companyId, _branchId, customerId, hasDelivery: false, SaleStatus.OnHold,
            [SaleDetail.Create(_battery.Id, quantity, 100000m, unitCost: 70000m)]);
        typeof(Sale).GetProperty(nameof(Sale.CreatedAt))!.SetValue(sale, createdAt);
        return sale;
    }

    private async Task<WholesaleByCustomerResponse> Run(string saleType, params Sale[] periodSales)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUser.SetupGet(s => s.CompanyId).Returns(_companyId);
        currentUser.SetupGet(s => s.CanViewAllBranches).Returns(true);
        var sales = new Mock<ISaleRepository>();
        sales.Setup(r => r.ListForSalesReportAsync(
                _companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(periodSales);
        sales.Setup(r => r.ListCcSalesByCompanyAsync(_companyId, null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.GetByCompanyIdAsync(_companyId, It.IsAny<CancellationToken>())).ReturnsAsync([_battery]);
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.ListByIdsAsync(_companyId, It.IsAny<IEnumerable<CustomerId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_frequent]);

        var result = await new WholesaleByCustomerHandler(currentUser.Object, sales.Object, products.Object, customers.Object)
            .Handle(new WholesaleByCustomerQuery(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), saleType), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }
}
