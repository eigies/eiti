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

    // Sale.Create fija CreatedAt = DateTime.UtcNow y no expone una fabrica para elegir la fecha.
    // Solo para probar la ventana ampliada de BuildDays (ver SerieDeSieteDias_TraeVentasDe...),
    // se ajusta CreatedAt por reflection despues de crear la venta.
    private static Sale Backdate(Sale sale, DateTime createdAt)
    {
        typeof(Sale).GetProperty(nameof(Sale.CreatedAt))!.SetValue(sale, createdAt);
        return sale;
    }

    private static GetDashboardSummaryHandler BuildHandler(
        Mock<ICurrentUserService> user,
        IReadOnlyList<Sale> sales,
        params Product[] products)
    {
        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(r => r.ListForSalesReportAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);
        saleRepository
            .Setup(r => r.CountCancelledAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var productRepository = new Mock<IProductRepository>();
        // Filtra por los ids pedidos, como haria la implementacion real: si el handler pidiera
        // el catalogo entero (GetByCompanyIdAsync) o los ids equivocados, esto lo expondria.
        productRepository
            .Setup(r => r.GetByIdsAsync(
                It.IsAny<IEnumerable<ProductId>>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ProductId> ids, CompanyId companyId, CancellationToken ct) =>
                (IReadOnlyList<Product>)products.Where(p => ids.Contains(p.Id)).ToList());

        var customerRepository = new Mock<ICustomerRepository>();
        customerRepository
            .Setup(r => r.ListByIdsAsync(
                It.IsAny<CompanyId>(), It.IsAny<IEnumerable<CustomerId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Customer>());

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
        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(r => r.ListForSalesReportAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Sin ventas no hay detalles que resolver: el handler nunca llega a pedir productos.
        var handler = new GetDashboardSummaryHandler(
            MockUser().Object, saleRepository.Object, new Mock<IProductRepository>().Object,
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
        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(r => r.ListForSalesReportAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Sin ventas no hay detalles que resolver: el handler nunca llega a pedir productos.
        var handler = new GetDashboardSummaryHandler(
            MockUser(canViewAll: false, allowedBranches: [BranchA.Value]).Object,
            saleRepository.Object, new Mock<IProductRepository>().Object,
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

    // Este test verifica que la serie tenga 7 puntos y que el mas nuevo tenga la venta, NO la
    // conversion de zona horaria: la venta se crea con CreatedAt = UtcNow y BuildDays arranca
    // el bucketing desde "hoy" resuelto con UtcNow, asi que una version con el bug de .Date
    // crudo pasaria igual salvo en la franja horaria en que el dia local y el dia UTC difieren.
    // La matematica del huso esta cubierta en BusinessCalendarTests.
    [Fact]
    public async Task SerieDeSieteDias_SiempreTieneSieteDias_YLosVaciosEnCero()
    {
        var product = SampleProduct();
        var handler = BuildHandler(MockUser(), [RetailSale(BranchA, product.Id, 100_000m)], product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.Days.Should().HaveCount(7);
        result.Value.Days.Should().BeInAscendingOrder(d => d.Date);
        // La venta es de hoy, asi que el ultimo punto la tiene y los anteriores estan en cero.
        result.Value.Days[^1].RetailCount.Should().Be(1);
        result.Value.Days[0].RetailCount.Should().Be(0);
        result.Value.Days[0].RetailAmount.Should().Be(0m);
    }

    [Fact]
    public async Task TopProductos_OrdenaPorUnidades()
    {
        // Con un solo producto en juego, OrderByDescending y OrderBy dan el mismo resultado:
        // hacen falta DOS productos con distinta cantidad de unidades para que el orden
        // importe de verdad.
        var winner = SampleProduct();
        var loser = Product.Create(
            Company, "CAB-001", "CAB-001", "Contoso", "Cable", null, 20_000m, 12_000m, null);
        var sales = new List<Sale>
        {
            RetailSale(BranchA, winner.Id, 100_000m),
            RetailSale(BranchA, winner.Id, 100_000m),
            RetailSale(BranchA, loser.Id, 20_000m)
        };
        var handler = BuildHandler(MockUser(), sales, winner, loser);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.TopProducts.Should().HaveCount(2);
        result.Value.TopProducts[0].ProductId.Should().Be(winner.Id.Value);
        result.Value.TopProducts[0].Units.Should().Be(2);
        result.Value.TopProducts[0].SalesCount.Should().Be(2);
        result.Value.TopProducts[0].Name.Should().Be("Bateria");
        result.Value.TopProducts[1].ProductId.Should().Be(loser.Id.Value);
        result.Value.TopProducts[1].Units.Should().Be(1);
        result.Value.TopProducts[1].Name.Should().Be("Cable");
    }

    [Fact]
    public async Task Cobranza_SeCalculaPorEstadoNoPorPagos()
    {
        var product = SampleProduct();
        // Sale.Create con SaleStatus.Paid => cobrada. Una CC nace OnHold => pendiente. Pero
        // RetailSale adjunta un SalePayment por el TOTAL, asi que con solo esas dos ventas una
        // implementacion basada en MonetaryPaidAmount (pagos) da los mismos numeros que una
        // basada en SaleStatus y el test pasa igual con el criterio roto.
        // Esta venta OnHold con pago PARCIAL es la que distingue los dos criterios: por estado
        // aporta 100.000 a pendiente; por pagos aportaria 30.000 a cobrado y 70.000 a pendiente.
        // allowOverpayment queda en false (default): Sale.SetSettlement usa
        // "allowOverpayment || SaleStatus.Paid" como requireAtLeastTotal, y con true el pago
        // parcial (30.000 < 100.000) tira InvalidOperationException al crear la venta.
        var partiallyPaidOnHold = Sale.Create(Company, BranchA, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(product.Id, 1, 100_000m)],
            [SalePayment.Create(SalePaymentMethod.Cash, 30_000m, null)]);
        var sales = new List<Sale>
        {
            RetailSale(BranchA, product.Id, 100_000m),
            CcSale(BranchA, CustomerId.New(), product.Id, 40_000m),
            partiallyPaidOnHold
        };
        var handler = BuildHandler(MockUser(), sales, product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.Collections.PaidAmount.Should().Be(100_000m);
        result.Value.Collections.PaidCount.Should().Be(1);
        result.Value.Collections.PendingAmount.Should().Be(140_000m);
        result.Value.Collections.PendingCount.Should().Be(2);
        result.Value.Collections.AvgTicket.Should().Be(80_000m);
    }

    [Fact]
    public async Task UltimasVentas_DevuelveComoMaximoSeisYLasMasRecientesPrimero()
    {
        var product = SampleProduct();
        var sales = Enumerable.Range(0, 8)
            .Select(_ => RetailSale(BranchA, product.Id, 10_000m))
            .ToList();
        var handler = BuildHandler(MockUser(), sales, product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.RecentSales.Should().HaveCount(6);
        result.Value.RecentSales.Should().BeInDescendingOrder(s => s.CreatedAt);
    }

    [Fact]
    public async Task CanceladasDeHoy_SalenDeLaConsultaAparte()
    {
        var product = SampleProduct();
        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessCalendar.TimeZone).Date;
        var expectedFrom = BusinessCalendar.StartOfDayUtc(todayLocal);
        var expectedTo = BusinessCalendar.EndOfDayUtc(todayLocal);

        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(r => r.ListForSalesReportAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([RetailSale(BranchA, product.Id, 100_000m)]);
        saleRepository
            .Setup(r => r.CountCancelledAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByIdsAsync(
                It.IsAny<IEnumerable<ProductId>>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        // Usuario restringido a BranchA: si CountCancelledAsync perdiera allowedBranchIds en el
        // camino, un usuario sin permiso de ver todas las sucursales veria canceladas ajenas.
        var handler = new GetDashboardSummaryHandler(
            MockUser(canViewAll: false, allowedBranches: [BranchA.Value]).Object,
            saleRepository.Object, productRepository.Object,
            new Mock<ICustomerRepository>().Object);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.TodayStatus.CancelledCount.Should().Be(3);
        result.Value.TodayStatus.ActiveCount.Should().Be(1);

        // El rango tiene que ser el de HOY, no el del mes pedido (1-31 ago), y allowedBranchIds
        // no puede perderse en el camino: con It.IsAny<> en todo esto pasaba igual aunque el
        // handler mandara el rango del mes o soltara el filtro de sucursal.
        saleRepository.Verify(r => r.CountCancelledAsync(
            It.IsAny<CompanyId>(),
            expectedFrom,
            expectedTo,
            null,
            It.Is<IReadOnlyCollection<Guid>?>(b => b != null && b.Count == 1 && b.Contains(BranchA.Value)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SerieDeSieteDias_TraeVentasDeAntesDelRangoPedido_SiCaenDentroDeLaVentanaDelGrafico()
    {
        var product = SampleProduct();
        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessCalendar.TimeZone).Date;

        // "Ayer" cae dentro de la ventana de 7 dias del grafico (hoy-6..hoy) pero ANTES del
        // rango pedido (mas abajo, solo "hoy"). Sin el fetch ampliado a ListForSalesReportAsync
        // esta venta nunca llega al handler y el punto de "ayer" sale en cero pese a existir.
        var yesterdaySale = Backdate(
            RetailSale(BranchA, product.Id, 55_000m),
            BusinessCalendar.StartOfDayUtc(todayLocal.AddDays(-1)).AddHours(12));
        var allStoredSales = new List<Sale> { yesterdaySale };

        // Mock que SI filtra por from/to (a diferencia de BuildHandler, que devuelve una lista
        // fija sin importar el rango): hace falta para poder distinguir el rango pedido del
        // rango ampliado, que es justo lo que este test verifica.
        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(r => r.ListForSalesReportAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyId companyId, DateTime from, DateTime to, Guid? branchId,
                    Guid? customerId, IReadOnlyCollection<Guid>? allowedBranchIds, CancellationToken ct) =>
                (IReadOnlyList<Sale>)allStoredSales
                    .Where(s => s.CreatedAt >= from && s.CreatedAt <= to)
                    .ToList());
        saleRepository
            .Setup(r => r.CountCancelledAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new GetDashboardSummaryHandler(
            MockUser().Object, saleRepository.Object,
            new Mock<IProductRepository>().Object, new Mock<ICustomerRepository>().Object);

        // Rango pedido = solo "hoy": fuerza a que "ayer" quede afuera de from..to pero adentro
        // de la ventana de 7 dias del grafico.
        var result = await handler.Handle(
            new GetDashboardSummaryQuery(todayLocal, todayLocal),
            CancellationToken.None);

        result.Value.Days[^2].RetailCount.Should().Be(1);
        result.Value.Days[^2].RetailAmount.Should().Be(55_000m);
        // El rango pedido no incluye "ayer": Month/Collections no deben contarla, solo la
        // serie de 7 dias puede mirar mas atras que el periodo pedido.
        result.Value.Month.Total.Count.Should().Be(0);
        result.Value.Collections.PaidCount.Should().Be(0);
    }

    [Fact]
    public async Task UltimasVentas_ResuelveElNombreDelClienteCuandoLoHay()
    {
        // BuildHandler mockea ICustomerRepository devolviendo Array.Empty<Customer>() por
        // defecto, asi que ningun otro test ejercita el camino feliz de la resolucion de
        // nombre: si el diccionario se rompiera (key equivocada, nunca se llama al repo), todas
        // las ventas dirian "Consumidor final" y la suite seguiria en verde.
        var product = SampleProduct();
        var customer = Customer.Create(Company, "Juana", "Perez", null);
        var sales = new List<Sale> { CcSale(BranchA, customer.Id, product.Id, 40_000m) };

        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(r => r.ListForSalesReportAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);
        saleRepository
            .Setup(r => r.CountCancelledAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByIdsAsync(
                It.IsAny<IEnumerable<ProductId>>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var customerRepository = new Mock<ICustomerRepository>();
        customerRepository
            .Setup(r => r.ListByIdsAsync(
                It.IsAny<CompanyId>(), It.IsAny<IEnumerable<CustomerId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([customer]);

        var handler = new GetDashboardSummaryHandler(
            MockUser().Object, saleRepository.Object, productRepository.Object, customerRepository.Object);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.RecentSales.Should().ContainSingle();
        result.Value.RecentSales[0].CustomerName.Should().Be("Juana Perez");
    }
}
