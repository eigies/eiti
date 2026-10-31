using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Reports.Queries.DailySalesControl;
using eiti.Application.Features.Reports.Queries.SalesReport;
using eiti.Application.Features.Sales.Common;
using eiti.Domain.Banks;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

/// <summary>
/// El recargo de la tarjeta cubre el costo de financiación: se cobra y se factura, pero no es venta ni ganancia.
/// Los reportes de ventas toman la mercadería (renglones / TotalAmount); los de medios de pago y caja, lo cobrado.
/// </summary>
public sealed class CardSurchargeProfitTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _branch;
    private readonly Product _battery;
    private readonly Sale _sale;

    public CardSurchargeProfitTests()
    {
        _branch = Branch.Create(_companyId, "Mataderos", "MAT", null);
        _battery = Product.Create(_companyId, "22GD", "22GD", "MOURA", "Bateria 12x65", null, 100000m, 70000m, null);
        var santander = Bank.Create(_companyId, "Santander");
        santander.UpsertInstallmentPlan(3, 15m, active: true);
        var card = SaleCardPayment.Charge(santander, 3, 100000m).CreatePayment(null);
        _sale = Sale.Create(_companyId, _branch.Id, null, false, SaleStatus.Paid,
            [SaleDetail.Create(_battery.Id, 1, 100000m, unitCost: 70000m)], [card],
            code: "MAT-5001", cardSurchargeTotal: 15000m);
    }

    [Fact]
    public async Task The_sales_report_counts_the_merchandise_and_not_the_card_surcharge()
    {
        var currentUser = CurrentUser();
        var sales = new Mock<ISaleRepository>();
        sales.Setup(r => r.ListForSalesReportAsync(
                _companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_sale]);
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.GetByCompanyIdAsync(_companyId, It.IsAny<CancellationToken>())).ReturnsAsync([_battery]);
        var transport = new Mock<ISaleTransportAssignmentRepository>();
        transport.Setup(r => r.ListBySaleIdsAsync(It.IsAny<IReadOnlyList<SaleId>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await new SalesReportHandler(currentUser.Object, sales.Object, products.Object, transport.Object,
                new Mock<IEmployeeRepository>().Object, new Mock<IVehicleRepository>().Object)
            .Handle(new SalesReportQuery(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), "total"), CancellationToken.None);

        _sale.EffectiveTotal.Should().Be(115000m, "la venta se cobró con el recargo");
        result.Value.Totals.Revenue.Should().Be(100000m);
        result.Value.Totals.Profit.Should().Be(30000m);
    }

    [Fact]
    public async Task The_daily_sales_control_totals_the_merchandise_and_shows_what_was_charged()
    {
        var sales = new Mock<ISaleRepository>();
        sales.Setup(r => r.ListByCompanyAsync(_companyId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([_sale]);
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.GetByCompanyIdAsync(_companyId, It.IsAny<CancellationToken>())).ReturnsAsync([_battery]);
        var branches = new Mock<IBranchRepository>();
        branches.Setup(r => r.ListByCompanyAsync(_companyId, It.IsAny<CancellationToken>())).ReturnsAsync([_branch]);

        var result = await new DailySalesControlHandler(CurrentUser().Object, sales.Object, products.Object,
                new Mock<ICustomerRepository>().Object, branches.Object)
            .Handle(new DailySalesControlQuery(DateTime.Today, DateTime.Today), CancellationToken.None);

        result.Value.Totals.TotalAmount.Should().Be(100000m);
        result.Value.Rows.Single().Payments.Single().Amount.Should().Be(115000m);
    }

    private Mock<ICurrentUserService> CurrentUser()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUser.SetupGet(s => s.CompanyId).Returns(_companyId);
        currentUser.SetupGet(s => s.CanViewAllBranches).Returns(true);
        return currentUser;
    }
}
