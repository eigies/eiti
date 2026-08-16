using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;

public sealed class GetDashboardSummaryHandler
    : IRequestHandler<GetDashboardSummaryQuery, Result<GetDashboardSummaryResponse>>
{
    private const int RecentSalesCount = 6;
    private const int TopProductsCount = 5;
    private const int ChartDays = 7;

    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly TimeProvider _timeProvider;

    public GetDashboardSummaryHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        ICustomerRepository customerRepository,
        TimeProvider timeProvider)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<GetDashboardSummaryResponse>> Handle(
        GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GetDashboardSummaryResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var canViewAllBranches = _currentUserService.CanViewAllBranches;
        var canViewFinancials = _currentUserService.HasPermission(
            PermissionCodes.DashboardViewFinancials);
        var allowedBranchIds = canViewAllBranches ? null : _currentUserService.AllowedBranchIds;

        // Que el selector no ofrezca una sucursal no alcanza: se valida en el server.
        if (request.BranchId.HasValue
            && !canViewAllBranches
            && !_currentUserService.AllowedBranchIds.Contains(request.BranchId.Value))
        {
            return Result<GetDashboardSummaryResponse>.Failure(GetDashboardSummaryErrors.BranchNotAllowed);
        }

        var (from, to) = BusinessCalendar.ToUtcRange(request.DateFrom, request.DateTo);
        var todayLocal = TodayLocal();

        // La serie del grafico mira 7 dias hacia atras desde hoy, que a principio de mes caen
        // en el mes anterior. Se pide el rango ampliado para que esos dias no salgan en cero.
        var chartFromUtc = BusinessCalendar.StartOfDayUtc(todayLocal.AddDays(-(ChartDays - 1)));
        var fetchFrom = from < chartFromUtc ? from : chartFromUtc;

        // La comparativa necesita el mes anterior completo. Se amplia el mismo fetch en vez de
        // hacer una consulta aparte: el filtrado por rango ya se hace en memoria mas abajo.
        var currentMonthStart = new DateTime(request.DateFrom.Year, request.DateFrom.Month, 1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var previousMonthFromUtc = BusinessCalendar.StartOfDayUtc(previousMonthStart);
        if (previousMonthFromUtc < fetchFrom)
        {
            fetchFrom = previousMonthFromUtc;
        }

        var allSales = await _saleRepository.ListForSalesReportAsync(
            companyId, fetchFrom, to, request.BranchId, null, allowedBranchIds, cancellationToken);

        // Month, Collections, TopProducts y RecentSales se calculan sobre el rango PEDIDO
        // (from..to), nunca sobre el ampliado: el fetch ampliado es solo insumo para que la
        // serie de 7 dias tenga datos reales cuando esos dias caen antes del inicio del periodo.
        var sales = allSales.Where(s => s.CreatedAt >= from && s.CreatedAt <= to).ToList();

        // Filtro de categoria: se resuelve producto -> categoria una sola vez y se reutiliza
        // para los totales, la serie y el top. Si no hay filtro, no se consulta el catalogo.
        var categoryFilter = request.CategoryIds is { Count: > 0 }
            ? request.CategoryIds.ToHashSet()
            : null;
        var categoryByProduct = categoryFilter is null
            ? new Dictionary<Guid, Guid?>()
            : await BuildCategoryLookupAsync(allSales, companyId, cancellationToken);

        // ListForSalesReportAsync excluye las canceladas por diseno; el conteo de canceladas
        // para el pulso del dia sale de CountCancelledAsync, una consulta COUNT aparte.
        var month = BuildTotals(sales, categoryFilter, categoryByProduct);

        var todayFrom = BusinessCalendar.StartOfDayUtc(todayLocal);
        var todayTo = BusinessCalendar.EndOfDayUtc(todayLocal);
        var todaySales = sales.Where(s => s.CreatedAt >= todayFrom && s.CreatedAt <= todayTo).ToList();
        var today = BuildTotals(todaySales, categoryFilter, categoryByProduct);

        var cancelledToday = await _saleRepository.CountCancelledAsync(
            companyId, todayFrom, todayTo, request.BranchId, allowedBranchIds, cancellationToken);

        var days = BuildDays(allSales, todayLocal, categoryFilter, categoryByProduct);
        var topProducts = await BuildTopProductsAsync(
            sales, companyId, categoryFilter, categoryByProduct, cancellationToken);
        var collections = BuildCollections(sales);
        var todayStatus = BuildTodayStatus(todaySales, cancelledToday);
        var recentSales = await BuildRecentSalesAsync(sales, companyId, cancellationToken);

        var monthComparison = BuildMonthComparison(
            allSales, currentMonthStart, previousMonthStart, todayLocal,
            categoryFilter, categoryByProduct);

        var response = new GetDashboardSummaryResponse(
            month, today, days, topProducts, collections, todayStatus, recentSales,
            monthComparison);

        return Result<GetDashboardSummaryResponse>.Success(
            canViewFinancials ? response : StripAmounts(response));
    }

    // La plata no se esconde solo en el front: sin permiso no sale del servidor ni queda
    // visible en el network tab. Las cantidades y los datos operativos se conservan.
    private static GetDashboardSummaryResponse StripAmounts(GetDashboardSummaryResponse source) =>
        source with
        {
            Month = StripTotals(source.Month),
            Today = StripTotals(source.Today),
            Days = source.Days
                .Select(d => d with { RetailAmount = 0m, CurrentAccountAmount = 0m })
                .ToList(),
            Collections = new DashboardCollections(
                0m, source.Collections.PaidCount, 0m, source.Collections.PendingCount, 0m),
            RecentSales = source.RecentSales
                .Select(s => s with { TotalAmount = 0m })
                .ToList(),
            MonthComparison = source.MonthComparison with
            {
                Current = StripCumulative(source.MonthComparison.Current),
                Previous = StripCumulative(source.MonthComparison.Previous)
            }
        };

    private static IReadOnlyList<DashboardCumulativePoint> StripCumulative(
        IReadOnlyList<DashboardCumulativePoint> points) =>
        points.Select(p => p with { Amount = 0m }).ToList();

    private static DashboardPeriodTotals StripTotals(DashboardPeriodTotals totals) =>
        new(
            totals.Total with { Amount = 0m },
            totals.Retail with { Amount = 0m },
            totals.CurrentAccount with { Amount = 0m });

    // El "hoy" del usuario, no el del servidor: se toma la fecha local segun BusinessCalendar.
    private DateTime TodayLocal() =>
        TimeZoneInfo.ConvertTimeFromUtc(
            _timeProvider.GetUtcNow().UtcDateTime,
            BusinessCalendar.TimeZone).Date;

    // Acumulado dia a dia de los dos meses, cortados en el mismo dia del mes.
    //
    // Si el periodo pedido es el mes en curso, el corte es hoy; si es un mes pasado, se toma
    // completo. Las dos series llegan siempre hasta el mismo dia para que la comparacion sea
    // honesta: julio entero contra agosto a mitad de camino diria que siempre se viene peor.
    private static DashboardMonthComparison BuildMonthComparison(
        IReadOnlyCollection<Sale> allSales,
        DateTime currentMonthStart,
        DateTime previousMonthStart,
        DateTime todayLocal,
        HashSet<Guid>? categoryFilter,
        IReadOnlyDictionary<Guid, Guid?> categoryByProduct)
    {
        var isCurrentMonth = todayLocal.Year == currentMonthStart.Year
            && todayLocal.Month == currentMonthStart.Month;

        var daysInCurrent = DateTime.DaysInMonth(currentMonthStart.Year, currentMonthStart.Month);
        var daysInPrevious = DateTime.DaysInMonth(previousMonthStart.Year, previousMonthStart.Month);

        // El corte no puede exceder los dias del mes mas corto: comparar el 31 de marzo contra
        // un 31 de febrero que no existe dejaria la serie anterior sin ese punto.
        var cutoff = isCurrentMonth ? todayLocal.Day : daysInCurrent;
        cutoff = Math.Min(cutoff, Math.Min(daysInCurrent, daysInPrevious));

        return new DashboardMonthComparison(
            DateOnly.FromDateTime(currentMonthStart),
            DateOnly.FromDateTime(previousMonthStart),
            cutoff,
            Cumulative(allSales, currentMonthStart, cutoff, categoryFilter, categoryByProduct),
            Cumulative(allSales, previousMonthStart, cutoff, categoryFilter, categoryByProduct));
    }

    private static IReadOnlyList<DashboardCumulativePoint> Cumulative(
        IReadOnlyCollection<Sale> allSales,
        DateTime monthStart,
        int cutoffDay,
        HashSet<Guid>? categoryFilter,
        IReadOnlyDictionary<Guid, Guid?> categoryByProduct)
    {
        var points = new List<DashboardCumulativePoint>(cutoffDay);
        var count = 0;
        var units = 0;
        var amount = 0m;

        for (var day = 1; day <= cutoffDay; day++)
        {
            var date = monthStart.AddDays(day - 1);
            var dayFrom = BusinessCalendar.StartOfDayUtc(date);
            var dayTo = BusinessCalendar.EndOfDayUtc(date);

            var ofDay = allSales
                .Where(s => s.CreatedAt >= dayFrom && s.CreatedAt <= dayTo)
                .ToList();

            var segment = Segment(ofDay, categoryFilter, categoryByProduct);
            count += segment.Count;
            units += segment.Units;
            amount += segment.Amount;

            points.Add(new DashboardCumulativePoint(
                day, count, units, decimal.Round(amount, 2, MidpointRounding.AwayFromZero)));
        }

        return points;
    }

    // Mapa productId -> categoryId de los productos que aparecen en las ventas del periodo.
    // Solo se llama cuando hay filtro de categoria: sin filtro no hace falta el catalogo.
    private async Task<Dictionary<Guid, Guid?>> BuildCategoryLookupAsync(
        IReadOnlyCollection<Sale> sales, CompanyId companyId, CancellationToken ct)
    {
        var productIds = sales
            .SelectMany(s => s.Details)
            .Select(d => d.ProductId)
            .Distinct()
            .ToList();

        if (productIds.Count == 0)
            return new Dictionary<Guid, Guid?>();

        return (await _productRepository.GetByIdsAsync(productIds, companyId, ct))
            .ToDictionary(p => p.Id.Value, p => p.CategoryId);
    }

    private static DashboardPeriodTotals BuildTotals(
        IReadOnlyCollection<Sale> sales,
        HashSet<Guid>? categoryFilter,
        IReadOnlyDictionary<Guid, Guid?> categoryByProduct)
    {
        var retail = sales.Where(s => !s.IsCuentaCorriente).ToList();
        var currentAccount = sales.Where(s => s.IsCuentaCorriente).ToList();

        return new DashboardPeriodTotals(
            Segment(sales, categoryFilter, categoryByProduct),
            Segment(retail, categoryFilter, categoryByProduct),
            Segment(currentAccount, categoryFilter, categoryByProduct));
    }

    // Una venta cuenta si aporta al menos una unidad de las categorias elegidas. Las unidades
    // y el importe suman solo las lineas que matchean, asi el numero es comparable con el
    // reporte de ventas filtrado por categoria.
    private static bool MatchesCategory(
        SaleDetail detail,
        HashSet<Guid> categoryFilter,
        IReadOnlyDictionary<Guid, Guid?> categoryByProduct)
        => categoryByProduct.TryGetValue(detail.ProductId.Value, out var categoryId)
            && categoryId.HasValue
            && categoryFilter.Contains(categoryId.Value);

    private static DashboardSegment Segment(
        IReadOnlyCollection<Sale> sales,
        HashSet<Guid>? categoryFilter,
        IReadOnlyDictionary<Guid, Guid?> categoryByProduct)
    {
        if (categoryFilter is null)
        {
            // Sin filtro: el importe es el total de la venta (incluye descuentos y recargos
            // generales, que no cuelgan de ninguna linea) y las unidades son todas.
            return new DashboardSegment(
                sales.Count,
                sales.Sum(s => s.Details.Sum(d => d.Quantity)),
                decimal.Round(sales.Sum(s => s.TotalAmount), 2, MidpointRounding.AwayFromZero));
        }

        var count = 0;
        var units = 0;
        var amount = 0m;

        foreach (var sale in sales)
        {
            var matching = sale.Details
                .Where(d => MatchesCategory(d, categoryFilter, categoryByProduct))
                .ToList();

            if (matching.Count == 0)
                continue;

            count++;
            units += matching.Sum(d => d.Quantity);
            amount += matching.Sum(d => d.TotalAmount);
        }

        return new DashboardSegment(
            count, units, decimal.Round(amount, 2, MidpointRounding.AwayFromZero));
    }

    // Siempre 7 puntos, del mas viejo al mas nuevo. Los dias sin ventas van en cero,
    // no ausentes: el grafico necesita el eje completo.
    // El filtro de categoria tambien manda aca: si no, el grafico contaria ventas que el
    // bloque de arriba no cuenta y los numeros no cerrarian entre si.
    private static IReadOnlyList<DashboardDayPoint> BuildDays(
        IReadOnlyCollection<Sale> sales,
        DateTime todayLocal,
        HashSet<Guid>? categoryFilter,
        IReadOnlyDictionary<Guid, Guid?> categoryByProduct)
    {
        var points = new List<DashboardDayPoint>(ChartDays);

        for (var offset = ChartDays - 1; offset >= 0; offset--)
        {
            var day = todayLocal.AddDays(-offset);
            var dayFrom = BusinessCalendar.StartOfDayUtc(day);
            var dayTo = BusinessCalendar.EndOfDayUtc(day);
            var ofDay = sales.Where(s => s.CreatedAt >= dayFrom && s.CreatedAt <= dayTo).ToList();

            var retail = Segment(
                ofDay.Where(s => !s.IsCuentaCorriente).ToList(), categoryFilter, categoryByProduct);
            var cc = Segment(
                ofDay.Where(s => s.IsCuentaCorriente).ToList(), categoryFilter, categoryByProduct);

            points.Add(new DashboardDayPoint(
                DateOnly.FromDateTime(day),
                retail.Count,
                retail.Amount,
                cc.Count,
                cc.Amount));
        }

        return points;
    }

    private async Task<IReadOnlyList<DashboardTopProduct>> BuildTopProductsAsync(
        IReadOnlyCollection<Sale> sales,
        CompanyId companyId,
        HashSet<Guid>? categoryFilter,
        IReadOnlyDictionary<Guid, Guid?> categoryByProduct,
        CancellationToken ct)
    {
        var accumulator = new Dictionary<Guid, (int Units, HashSet<Guid> SaleIds)>();

        foreach (var sale in sales)
        {
            foreach (var detail in sale.Details)
            {
                if (categoryFilter is not null
                    && !MatchesCategory(detail, categoryFilter, categoryByProduct))
                {
                    continue;
                }

                var key = detail.ProductId.Value;
                if (!accumulator.TryGetValue(key, out var acc))
                {
                    acc = (0, new HashSet<Guid>());
                }

                acc.Units += detail.Quantity;
                acc.SaleIds.Add(sale.Id.Value);
                accumulator[key] = acc;
            }
        }

        if (accumulator.Count == 0)
            return Array.Empty<DashboardTopProduct>();

        // Se recorta a los TopProductsCount ganadores ANTES de pedir nombres: GetByCompanyIdAsync
        // trae el catalogo entero al change tracker en cada carga del dashboard, GetByIdsAsync
        // trae solo los que se van a mostrar.
        var top = accumulator
            .OrderByDescending(kvp => kvp.Value.Units)
            .ThenByDescending(kvp => kvp.Value.SaleIds.Count)
            .Take(TopProductsCount)
            .ToList();

        var productIds = top.Select(kvp => new ProductId(kvp.Key)).ToList();
        var products = (await _productRepository.GetByIdsAsync(productIds, companyId, ct))
            .ToDictionary(p => p.Id.Value, p => p);

        return top
            .Select(kvp =>
            {
                products.TryGetValue(kvp.Key, out var product);
                return new DashboardTopProduct(
                    kvp.Key,
                    product?.Name ?? "Producto eliminado",
                    product?.Brand ?? "Sin marca",
                    kvp.Value.Units,
                    kvp.Value.SaleIds.Count);
            })
            // Desempate final por nombre: sin esto, dos productos empatados en unidades y
            // cantidad de ventas quedan en un orden que depende de la enumeracion del
            // diccionario, no del dato.
            .OrderByDescending(p => p.Units)
            .ThenByDescending(p => p.SalesCount)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
    }

    // Cobrado y pendiente salen del ESTADO de la venta, no de sus pagos: Sale.MonetaryPaidAmount
    // es _payments.Sum(...) y sin Include(Payments) daria 0. Mismo criterio que el dashboard viejo.
    private static DashboardCollections BuildCollections(IReadOnlyCollection<Sale> sales)
    {
        var paid = sales.Where(s => s.SaleStatus == SaleStatus.Paid).ToList();
        var pending = sales.Where(s => s.SaleStatus == SaleStatus.OnHold).ToList();
        var total = sales.Sum(s => s.TotalAmount);

        return new DashboardCollections(
            decimal.Round(paid.Sum(s => s.TotalAmount), 2, MidpointRounding.AwayFromZero),
            paid.Count,
            decimal.Round(pending.Sum(s => s.TotalAmount), 2, MidpointRounding.AwayFromZero),
            pending.Count,
            sales.Count == 0 ? 0m : decimal.Round(total / sales.Count, 2, MidpointRounding.AwayFromZero));
    }

    // OJO: cancelledCount NO puede salir de todaySales. ListForSalesReportAsync filtra
    // SaleStatus != Cancel, asi que las canceladas nunca llegan y el contador daria siempre 0.
    // Viene de una consulta COUNT aparte (ver Step 4 de esta tarea).
    private static DashboardTodayStatus BuildTodayStatus(
        IReadOnlyCollection<Sale> todaySales, int cancelledCount) =>
        new(todaySales.Count,
            todaySales.Count(s => s.SaleStatus == SaleStatus.Paid),
            todaySales.Count(s => s.SaleStatus == SaleStatus.OnHold),
            cancelledCount);

    private async Task<IReadOnlyList<DashboardRecentSale>> BuildRecentSalesAsync(
        IReadOnlyCollection<Sale> sales, CompanyId companyId, CancellationToken ct)
    {
        var recent = sales
            .OrderByDescending(s => s.CreatedAt)
            .Take(RecentSalesCount)
            .ToList();

        if (recent.Count == 0)
            return Array.Empty<DashboardRecentSale>();

        var customerIds = recent
            .Where(s => s.CustomerId is not null)
            .Select(s => s.CustomerId!)
            .Distinct()
            .ToList();

        var customerNames = customerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _customerRepository.ListByIdsAsync(companyId, customerIds, ct))
                .ToDictionary(c => c.Id.Value, c => c.FullName);

        return recent
            .Select(s => new DashboardRecentSale(
                s.Id.Value,
                s.Code,
                s.CreatedAt,
                s.CustomerId is not null && customerNames.TryGetValue(s.CustomerId.Value, out var name)
                    ? name
                    : "Consumidor final",
                (int)s.SaleStatus,
                s.TotalAmount,
                s.IsCuentaCorriente))
            .ToList();
    }
}
