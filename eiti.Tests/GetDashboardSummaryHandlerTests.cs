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
    private static readonly DateTime FixedTodayLocal = new(2026, 8, 15);
    private static readonly DateTime FixedCreatedAtUtc =
        BusinessCalendar.StartOfDayUtc(FixedTodayLocal).AddHours(12);
    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(FixedCreatedAtUtc));

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
        Backdate(Sale.Create(Company, branchId, null, false, SaleStatus.Paid,
            [SaleDetail.Create(productId, 1, price)],
            [SalePayment.Create(SalePaymentMethod.Cash, price, null)],
            allowOverpayment: true), FixedCreatedAtUtc);

    private static Sale CcSale(BranchId branchId, CustomerId customerId, ProductId productId, decimal price) =>
        Backdate(Sale.CreateCc(Company, branchId, customerId,
            [SaleDetail.Create(productId, 1, price)]), FixedCreatedAtUtc);

    // Sale.Create no expone una fabrica para elegir la fecha. Los fixtures se fijan al reloj del
    // handler por reflection para que esta suite siga siendo valida despues de agosto de 2026.
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
            user.Object, saleRepository.Object, productRepository.Object, customerRepository.Object, Clock);
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

    // El reloj y la venta estan fijados al mismo dia local; la matematica del huso esta cubierta
    // en BusinessCalendarTests y el uso del rango se verifica en el test siguiente.
    [Fact]
    public async Task VentasDeHoy_QuedanEnLaColumnaHoy()
    {
        var product = SampleProduct();
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
            new Mock<ICustomerRepository>().Object, Clock);

        await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        // El handler pide un rango AMPLIADO: retrocede al inicio del mes anterior para poder
        // armar la comparativa, y el extremo final sigue siendo el del periodo pedido.
        // Lo que este test fija es que los dos limites salgan de BusinessCalendar: el dia local
        // arranca a las 03:00 UTC, con .Date crudo seria 00:00 y el Verify no matchea.
        var expectedFrom = BusinessCalendar.StartOfDayUtc(new DateTime(2026, 7, 1));
        var expectedTo = BusinessCalendar.EndOfDayUtc(new DateTime(2026, 8, 31));

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
            new Mock<ICustomerRepository>().Object, Clock);

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

    // El reloj fijo hace que la serie sea determinista sin depender del dia en que corre la suite.
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
        Backdate(partiallyPaidOnHold, FixedCreatedAtUtc);
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
        var todayLocal = FixedTodayLocal;
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
            new Mock<ICustomerRepository>().Object, Clock);

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
        var todayLocal = FixedTodayLocal;

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
            new Mock<IProductRepository>().Object, new Mock<ICustomerRepository>().Object, Clock);

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
            MockUser().Object, saleRepository.Object, productRepository.Object, customerRepository.Object, Clock);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.RecentSales.Should().ContainSingle();
        result.Value.RecentSales[0].CustomerName.Should().Be("Juana Perez");
    }

    [Fact]
    public async Task SinPermisoFinanciero_LosImportesSalenEnCero_PeroLasCantidadesNo()
    {
        var product = SampleProduct();
        var sales = new List<Sale>
        {
            RetailSale(BranchA, product.Id, 100_000m),
            CcSale(BranchA, CustomerId.New(), product.Id, 40_000m)
        };
        var handler = BuildHandler(MockUser(canViewFinancials: false), sales, product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.Month.Total.Count.Should().Be(2);
        result.Value.Month.Retail.Count.Should().Be(1);
        result.Value.Month.Total.Amount.Should().Be(0m);
        result.Value.Month.Retail.Amount.Should().Be(0m);
        result.Value.Month.CurrentAccount.Amount.Should().Be(0m);
        result.Value.Today.Total.Amount.Should().Be(0m);
        result.Value.Collections.PaidAmount.Should().Be(0m);
        result.Value.Collections.PendingAmount.Should().Be(0m);
        result.Value.Collections.AvgTicket.Should().Be(0m);
        result.Value.Days.Should().OnlyContain(
            d => d.RetailAmount == 0m && d.CurrentAccountAmount == 0m);
        result.Value.RecentSales.Should().OnlyContain(s => s.TotalAmount == 0m);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    // ---- Comparativa contra el mes anterior ----

    [Fact]
    public async Task Comparativa_CortaLasDosSeriesElMismoDia()
    {
        var product = SampleProduct();
        var handler = BuildHandler(MockUser(), [RetailSale(BranchA, product.Id, 100_000m)], product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        var comparison = result.Value.MonthComparison;

        // El reloj de la suite esta fijado dentro de agosto, asi que el corte es el dia de hoy
        // y NO agosto completo: comparar un mes entero contra uno a mitad de camino diria
        // siempre que se viene peor.
        comparison.CurrentMonth.Should().Be(new DateOnly(2026, 8, 1));
        comparison.PreviousMonth.Should().Be(new DateOnly(2026, 7, 1));
        comparison.DaysElapsed.Should().BeLessThan(31);
        comparison.Current.Should().HaveCount(comparison.DaysElapsed);
        comparison.Previous.Should().HaveCount(comparison.DaysElapsed);
    }

    [Fact]
    public async Task Comparativa_AcumulaEnVezDeMostrarElMovimientoDelDia()
    {
        var product = SampleProduct();
        var handler = BuildHandler(
            MockUser(),
            [RetailSale(BranchA, product.Id, 100_000m), RetailSale(BranchA, product.Id, 50_000m)],
            product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        var current = result.Value.MonthComparison.Current;

        // Serie acumulada: nunca baja, y el ultimo punto es el total del tramo.
        current.Should().BeInAscendingOrder(p => p.Count);
        current[^1].Count.Should().Be(2);
        current[^1].Units.Should().Be(2);
        current[^1].Amount.Should().Be(150_000m);
    }

    [Fact]
    public async Task Comparativa_SinPermisoFinanciero_NoDevuelveImportes()
    {
        var product = SampleProduct();
        var handler = BuildHandler(
            MockUser(canViewFinancials: false), [RetailSale(BranchA, product.Id, 100_000m)], product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.MonthComparison.Current.Should().OnlyContain(p => p.Amount == 0m);
        result.Value.MonthComparison.Previous.Should().OnlyContain(p => p.Amount == 0m);
        // Las cantidades se conservan: el vendedor sigue viendo su volumen.
        result.Value.MonthComparison.Current[^1].Count.Should().Be(1);
    }

    // ---- Filtro por categoria ----
    // El dashboard contaba ventas y el reporte contaba unidades, asi que nunca coincidian:
    // 280 ventas contra 272 baterias. Ahora el segmento devuelve las dos cosas y se puede
    // acotar a las categorias que al usuario le interesan.

    private static readonly Guid CategoriaBateria = Guid.NewGuid();
    private static readonly Guid CategoriaAccesorios = Guid.NewGuid();

    private static Product ProductoDeCategoria(string code, string name, Guid? categoryId) =>
        Product.Create(Company, code, code, "Contoso", name, null, 100_000m, 70_000m, null,
            categoryId: categoryId);

    // Una venta con dos baterias y un accesorio: 3 unidades en total, 2 de bateria.
    private static Sale VentaMixta(ProductId bateria, ProductId accesorio) =>
        Backdate(Sale.Create(Company, BranchA, null, false, SaleStatus.Paid,
            [SaleDetail.Create(bateria, 2, 100_000m), SaleDetail.Create(accesorio, 1, 20_000m)],
            [SalePayment.Create(SalePaymentMethod.Cash, 220_000m, null)],
            allowOverpayment: true), FixedCreatedAtUtc);

    [Fact]
    public async Task SinFiltroDeCategoria_CuentaVentasYUnidadesDeTodoElCatalogo()
    {
        var bateria = ProductoDeCategoria("BAT-1", "Bateria", CategoriaBateria);
        var accesorio = ProductoDeCategoria("ACC-1", "Accesorio", CategoriaAccesorios);
        var handler = BuildHandler(
            MockUser(), [VentaMixta(bateria.Id, accesorio.Id)], bateria, accesorio);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.Month.Total.Count.Should().Be(1);
        result.Value.Month.Total.Units.Should().Be(3);
        result.Value.Month.Total.Amount.Should().Be(220_000m);
    }

    [Fact]
    public async Task ConFiltroDeCategoria_SoloCuentaLasUnidadesDeEsasCategorias()
    {
        var bateria = ProductoDeCategoria("BAT-1", "Bateria", CategoriaBateria);
        var accesorio = ProductoDeCategoria("ACC-1", "Accesorio", CategoriaAccesorios);
        var handler = BuildHandler(
            MockUser(), [VentaMixta(bateria.Id, accesorio.Id)], bateria, accesorio);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(
                new DateTime(2026, 8, 1), new DateTime(2026, 8, 31),
                CategoryIds: [CategoriaBateria]),
            CancellationToken.None);

        // La venta lleva bateria, asi que cuenta una vez aunque tambien tenga un accesorio.
        result.Value.Month.Total.Count.Should().Be(1);
        // Solo las 2 baterias, no las 3 unidades.
        result.Value.Month.Total.Units.Should().Be(2);
        // Solo el importe de la linea de bateria.
        result.Value.Month.Total.Amount.Should().Be(200_000m);
    }

    [Fact]
    public async Task ConFiltroDeCategoria_LaVentaSinEsaCategoriaNoCuenta()
    {
        var bateria = ProductoDeCategoria("BAT-1", "Bateria", CategoriaBateria);
        var accesorio = ProductoDeCategoria("ACC-1", "Accesorio", CategoriaAccesorios);
        var soloAccesorio = Backdate(Sale.Create(Company, BranchA, null, false, SaleStatus.Paid,
            [SaleDetail.Create(accesorio.Id, 4, 20_000m)],
            [SalePayment.Create(SalePaymentMethod.Cash, 80_000m, null)],
            allowOverpayment: true), FixedCreatedAtUtc);

        var handler = BuildHandler(MockUser(), [soloAccesorio], bateria, accesorio);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(
                new DateTime(2026, 8, 1), new DateTime(2026, 8, 31),
                CategoryIds: [CategoriaBateria]),
            CancellationToken.None);

        result.Value.Month.Total.Count.Should().Be(0);
        result.Value.Month.Total.Units.Should().Be(0);
        result.Value.Month.Total.Amount.Should().Be(0m);
    }

    [Fact]
    public async Task ConFiltroDeCategoria_ElTopDeProductosTambienSeAcota()
    {
        var bateria = ProductoDeCategoria("BAT-1", "Bateria", CategoriaBateria);
        var accesorio = ProductoDeCategoria("ACC-1", "Accesorio", CategoriaAccesorios);
        var handler = BuildHandler(
            MockUser(), [VentaMixta(bateria.Id, accesorio.Id)], bateria, accesorio);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(
                new DateTime(2026, 8, 1), new DateTime(2026, 8, 31),
                CategoryIds: [CategoriaBateria]),
            CancellationToken.None);

        // Si el top ignorara el filtro, el accesorio apareceria en la lista.
        result.Value.TopProducts.Should().HaveCount(1);
        result.Value.TopProducts[0].ProductId.Should().Be(bateria.Id.Value);
        result.Value.TopProducts[0].Units.Should().Be(2);
    }
}
