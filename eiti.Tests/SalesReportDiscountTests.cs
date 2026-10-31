using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Reports.Queries.SalesReport;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

// El reporte de rentabilidad tomaba el ingreso de los renglones sin el descuento general: una venta con
// descuento figuraba con ingreso y ganancia inflados. El descuento se prorratea en cada renglón.
public sealed class SalesReportDiscountTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly BranchId _branchId = BranchId.New();
    private readonly Product _battery;
    private readonly Product _cable;
    private readonly Mock<ISaleRepository> _sales = new();

    public SalesReportDiscountTests()
    {
        _battery = Product.Create(_companyId, "BAT-001", "BAT-001", "Moura", "Bateria 12x65", null, 100000m, 70000m, null);
        _cable = Product.Create(_companyId, "CAB-001", "CAB-001", "Generico", "Cable", null, 3333m, 1000m, null);
    }

    [Fact]
    public async Task ASaleWithTenPercentOff_CountsTenPercentLessRevenue()
    {
        var sale = Sale(generalDiscount: 10m, SaleDetail.Create(_battery.Id, 1, 100000m, unitCost: 70000m));

        var totals = (await Run(sale)).Totals;

        totals.Revenue.Should().Be(90000m);
        totals.Cost.Should().Be(70000m);
        totals.Profit.Should().Be(20000m);
    }

    [Fact]
    public async Task TheDiscountIsProratedOnEveryLine_RoundedToCents()
    {
        var sale = Sale(generalDiscount: 10m,
            SaleDetail.Create(_battery.Id, 1, 100000m, unitCost: 70000m),
            SaleDetail.Create(_cable.Id, 1, 3333m, unitCost: 1000m));

        var rows = (await Run(sale, groupBy: "product")).Rows;

        rows.Single(r => r.Key == _battery.Id.Value.ToString()).Revenue.Should().Be(90000m);
        rows.Single(r => r.Key == _cable.Id.Value.ToString()).Revenue.Should().Be(2999.70m);
    }

    [Fact]
    public async Task ASaleWithoutDiscount_KeepsItsRevenue()
    {
        var sale = Sale(generalDiscount: 0m, SaleDetail.Create(_battery.Id, 1, 100000m, unitCost: 70000m));

        (await Run(sale)).Totals.Revenue.Should().Be(100000m);
    }

    [Fact]
    public void ProratedLineAmount_AppliesTheGeneralDiscountToTheLine()
    {
        var cable = SaleDetail.Create(_cable.Id, 1, 3333m);
        var sale = Sale(generalDiscount: 10m, SaleDetail.Create(_battery.Id, 1, 100000m), cable);

        sale.ProratedLineAmount(cable).Should().Be(2999.70m);
    }

    private Sale Sale(decimal generalDiscount, params SaleDetail[] details) =>
        eiti.Domain.Sales.Sale.Create(_companyId, _branchId, null, false, SaleStatus.OnHold, details, generalDiscountPercent: generalDiscount);

    private async Task<SalesReportResponse> Run(Sale sale, string groupBy = "total")
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUser.SetupGet(s => s.CompanyId).Returns(_companyId);
        currentUser.SetupGet(s => s.CanViewAllBranches).Returns(true);
        _sales.Setup(r => r.ListForSalesReportAsync(
                _companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([sale]);
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.GetByCompanyIdAsync(_companyId, It.IsAny<CancellationToken>())).ReturnsAsync([_battery, _cable]);
        var transport = new Mock<ISaleTransportAssignmentRepository>();
        transport.Setup(r => r.ListBySaleIdsAsync(It.IsAny<IReadOnlyList<SaleId>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new SalesReportHandler(
            currentUser.Object, _sales.Object, products.Object, transport.Object,
            new Mock<IEmployeeRepository>().Object, new Mock<IVehicleRepository>().Object);

        var result = await handler.Handle(
            new SalesReportQuery(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), groupBy), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }
}
