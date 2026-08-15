using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class GetDashboardSummaryHandlerTests
{
    private static readonly CompanyId Company = CompanyId.New();
    private static readonly BranchId BranchA = BranchId.New();
    private static readonly BranchId BranchB = BranchId.New();

    private static Mock<ICurrentUserService> MockUser(
        bool canViewAll = true,
        bool canViewFinancials = true,
        IReadOnlyCollection<Guid>? allowedBranches = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(Company);
        user.SetupGet(u => u.CanViewAllBranches).Returns(canViewAll);
        user.SetupGet(u => u.AllowedBranchIds).Returns(allowedBranches ?? Array.Empty<Guid>());
        user.Setup(u => u.HasPermission(PermissionCodes.DashboardViewFinancials))
            .Returns(canViewFinancials);
        return user;
    }

    private static Product SampleProduct() =>
        Product.Create(Company, "BAT-001", "BAT-001", "Contoso", "Bateria", null, 100_000m, 70_000m, null);

    private static Sale RetailSale(BranchId branchId, ProductId productId, decimal price) =>
        Sale.Create(Company, branchId, null, false, SaleStatus.Paid,
            [SaleDetail.Create(productId, 1, price)],
            [SalePayment.Create(SalePaymentMethod.Cash, price, null)],
            allowOverpayment: true);

    private static Sale CcSale(BranchId branchId, CustomerId customerId, ProductId productId, decimal price) =>
        Sale.CreateCc(Company, branchId, customerId, [SaleDetail.Create(productId, 1, price)]);

    private static GetDashboardSummaryHandler BuildHandler(
        Mock<ICurrentUserService> user,
        IReadOnlyList<Sale> sales,
        Product product)
    {
        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(r => r.ListForSalesReportAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByCompanyIdAsync(It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var customerRepository = new Mock<ICustomerRepository>();

        return new GetDashboardSummaryHandler(
            user.Object, saleRepository.Object, productRepository.Object, customerRepository.Object);
    }

    [Fact]
    public async Task SepararMinoristaDeCuentaCorriente()
    {
        var product = SampleProduct();
        var customer = CustomerId.New();
        var sales = new List<Sale>
        {
            RetailSale(BranchA, product.Id, 100_000m),
            RetailSale(BranchA, product.Id, 50_000m),
            CcSale(BranchA, customer, product.Id, 30_000m)
        };
        var handler = BuildHandler(MockUser(), sales, product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Month.Total.Count.Should().Be(3);
        result.Value.Month.Total.Amount.Should().Be(180_000m);
        result.Value.Month.Retail.Count.Should().Be(2);
        result.Value.Month.Retail.Amount.Should().Be(150_000m);
        result.Value.Month.CurrentAccount.Count.Should().Be(1);
        result.Value.Month.CurrentAccount.Amount.Should().Be(30_000m);
    }

    [Fact]
    public async Task SucursalAjena_SinPermisoDeVerTodas_EsRechazada()
    {
        var product = SampleProduct();
        var handler = BuildHandler(
            MockUser(canViewAll: false, allowedBranches: [BranchA.Value]),
            [],
            product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(
                new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), BranchB.Value),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Dashboard.Summary.BranchNotAllowed");
    }

    [Fact]
    public async Task SucursalPropia_SinPermisoDeVerTodas_EsAceptada()
    {
        var product = SampleProduct();
        var handler = BuildHandler(
            MockUser(canViewAll: false, allowedBranches: [BranchA.Value]),
            [RetailSale(BranchA, product.Id, 100_000m)],
            product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(
                new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), BranchA.Value),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Month.Total.Count.Should().Be(1);
    }

    // Este test verifica el ruteo mes/hoy, NO la conversion de zona horaria: la venta se crea
    // con CreatedAt = UtcNow y el handler resuelve "hoy" desde UtcNow, asi que una version con
    // el bug de .Date crudo pasaria igual. La matematica del huso esta cubierta en
    // BusinessCalendarTests, y que el handler la use se verifica en
    // ElRangoDelMes_SeConvierteConBusinessCalendar_NoConFechaCruda.
    [Fact]
    public async Task VentasDeHoy_QuedanEnLaColumnaHoy()
    {
        var product = SampleProduct();
        // Sale.Create pone CreatedAt = UtcNow, o sea que las ventas del set son "de hoy".
        var handler = BuildHandler(MockUser(), [RetailSale(BranchA, product.Id, 100_000m)], product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.Today.Total.Count.Should().Be(1);
        result.Value.Today.Retail.Count.Should().Be(1);
        result.Value.Today.CurrentAccount.Count.Should().Be(0);
    }

    [Fact]
    public async Task ElRangoDelMes_SeConvierteConBusinessCalendar_NoConFechaCruda()
    {
        var product = SampleProduct();
        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(r => r.ListForSalesReportAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByCompanyIdAsync(It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var handler = new GetDashboardSummaryHandler(
            MockUser().Object, saleRepository.Object, productRepository.Object,
            new Mock<ICustomerRepository>().Object);

        await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        // El dia local arranca a las 03:00 UTC. Con .Date crudo esto seria 00:00 y el test falla.
        var (expectedFrom, expectedTo) = BusinessCalendar.ToUtcRange(
            new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        saleRepository.Verify(r => r.ListForSalesReportAsync(
            It.IsAny<CompanyId>(),
            expectedFrom,
            expectedTo,
            It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SinSucursalPedida_UsuarioRestringido_AcotaPorSusSucursalesPermitidas()
    {
        var product = SampleProduct();
        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(r => r.ListForSalesReportAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByCompanyIdAsync(It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var handler = new GetDashboardSummaryHandler(
            MockUser(canViewAll: false, allowedBranches: [BranchA.Value]).Object,
            saleRepository.Object, productRepository.Object,
            new Mock<ICustomerRepository>().Object);

        await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        // Sin este filtro un usuario restringido veria las ventas de todas las sucursales.
        saleRepository.Verify(r => r.ListForSalesReportAsync(
            It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            null,
            It.IsAny<Guid?>(),
            It.Is<IReadOnlyCollection<Guid>?>(b => b != null && b.Count == 1 && b.Contains(BranchA.Value)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
