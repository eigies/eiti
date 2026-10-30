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

// Mismo bug que el reporte de rentabilidad, en el reporte mayorista por cliente: el ingreso salía de los
// renglones sin el descuento general. Las ventas CC son justamente las que tienen descuento general.
public sealed class WholesaleByCustomerDiscountTests
{
    [Fact]
    public async Task ACcSaleWithTenPercentOff_CountsTenPercentLessRevenue()
    {
        var companyId = CompanyId.New();
        var battery = Product.Create(companyId, "BAT-001", "BAT-001", "Moura", "Bateria 12x65", null, 100000m, 70000m, null);
        var customer = Customer.Create(companyId, "Juan", "Perez", null);
        var sale = Sale.CreateCc(companyId, BranchId.New(), customer.Id,
            [SaleDetail.Create(battery.Id, 1, 100000m, unitCost: 70000m)], generalDiscountPercent: 10m);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUser.SetupGet(s => s.CompanyId).Returns(companyId);
        currentUser.SetupGet(s => s.CanViewAllBranches).Returns(true);
        var sales = new Mock<ISaleRepository>();
        sales.Setup(r => r.ListForSalesReportAsync(
                companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([sale]);
        sales.Setup(r => r.ListCcSalesByCompanyAsync(companyId, null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.GetByCompanyIdAsync(companyId, It.IsAny<CancellationToken>())).ReturnsAsync([battery]);
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.ListByIdsAsync(companyId, It.IsAny<IEnumerable<CustomerId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([customer]);

        var result = await new WholesaleByCustomerHandler(currentUser.Object, sales.Object, products.Object, customers.Object)
            .Handle(new WholesaleByCustomerQuery(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)), CancellationToken.None);

        var row = result.Value.Rows.Should().ContainSingle().Subject;
        row.Revenue.Should().Be(90000m);
        row.Cost.Should().Be(70000m);
        row.Profit.Should().Be(20000m);
    }
}
