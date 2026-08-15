using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
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

    public GetDashboardSummaryHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        ICustomerRepository customerRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<GetDashboardSummaryResponse>> Handle(
        GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GetDashboardSummaryResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var canViewAllBranches = _currentUserService.CanViewAllBranches;
        var allowedBranchIds = canViewAllBranches ? null : _currentUserService.AllowedBranchIds;

        // Que el selector no ofrezca una sucursal no alcanza: se valida en el server.
        if (request.BranchId.HasValue
            && !canViewAllBranches
            && !_currentUserService.AllowedBranchIds.Contains(request.BranchId.Value))
        {
            return Result<GetDashboardSummaryResponse>.Failure(GetDashboardSummaryErrors.BranchNotAllowed);
        }

        var (from, to) = BusinessCalendar.ToUtcRange(request.DateFrom, request.DateTo);

        var sales = await _saleRepository.ListForSalesReportAsync(
            companyId, from, to, request.BranchId, null, allowedBranchIds, cancellationToken);

        // ListForSalesReportAsync excluye las canceladas por diseno. El conteo de canceladas
        // para el pulso del dia se agrega en la tarea siguiente, con una consulta aparte.
        var month = BuildTotals(sales);

        var todayLocal = TodayLocal();
        var todayFrom = BusinessCalendar.StartOfDayUtc(todayLocal);
        var todayTo = BusinessCalendar.EndOfDayUtc(todayLocal);
        var todaySales = sales.Where(s => s.CreatedAt >= todayFrom && s.CreatedAt <= todayTo).ToList();
        var today = BuildTotals(todaySales);

        var cancelledToday = await _saleRepository.CountCancelledAsync(
            companyId, todayFrom, todayTo, request.BranchId, allowedBranchIds, cancellationToken);

        var days = BuildDays(sales, todayLocal);
        var topProducts = await BuildTopProductsAsync(sales, companyId, cancellationToken);
        var collections = BuildCollections(sales);
        var todayStatus = BuildTodayStatus(todaySales, cancelledToday);
        var recentSales = await BuildRecentSalesAsync(sales, companyId, cancellationToken);

        return Result<GetDashboardSummaryResponse>.Success(new GetDashboardSummaryResponse(
            month, today, days, topProducts, collections, todayStatus, recentSales));
    }

    // El "hoy" del usuario, no el del servidor: se toma la fecha local segun BusinessCalendar.
    private static DateTime TodayLocal() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessCalendar.TimeZone).Date;

    private static DashboardPeriodTotals BuildTotals(IReadOnlyCollection<Sale> sales)
    {
        var retail = sales.Where(s => !s.IsCuentaCorriente).ToList();
        var currentAccount = sales.Where(s => s.IsCuentaCorriente).ToList();

        return new DashboardPeriodTotals(
            Segment(sales),
            Segment(retail),
            Segment(currentAccount));
    }

    private static DashboardSegment Segment(IReadOnlyCollection<Sale> sales) =>
        new(sales.Count, decimal.Round(sales.Sum(s => s.TotalAmount), 2, MidpointRounding.AwayFromZero));

    // Siempre 7 puntos, del mas viejo al mas nuevo. Los dias sin ventas van en cero,
    // no ausentes: el grafico necesita el eje completo.
    private static IReadOnlyList<DashboardDayPoint> BuildDays(
        IReadOnlyCollection<Sale> sales, DateTime todayLocal)
    {
        var points = new List<DashboardDayPoint>(ChartDays);

        for (var offset = ChartDays - 1; offset >= 0; offset--)
        {
            var day = todayLocal.AddDays(-offset);
            var dayFrom = BusinessCalendar.StartOfDayUtc(day);
            var dayTo = BusinessCalendar.EndOfDayUtc(day);
            var ofDay = sales.Where(s => s.CreatedAt >= dayFrom && s.CreatedAt <= dayTo).ToList();

            var retail = ofDay.Where(s => !s.IsCuentaCorriente).ToList();
            var cc = ofDay.Where(s => s.IsCuentaCorriente).ToList();

            points.Add(new DashboardDayPoint(
                day,
                retail.Count,
                decimal.Round(retail.Sum(s => s.TotalAmount), 2, MidpointRounding.AwayFromZero),
                cc.Count,
                decimal.Round(cc.Sum(s => s.TotalAmount), 2, MidpointRounding.AwayFromZero)));
        }

        return points;
    }

    private async Task<IReadOnlyList<DashboardTopProduct>> BuildTopProductsAsync(
        IReadOnlyCollection<Sale> sales, Domain.Companies.CompanyId companyId, CancellationToken ct)
    {
        var accumulator = new Dictionary<Guid, (int Units, HashSet<Guid> SaleIds)>();

        foreach (var sale in sales)
        {
            foreach (var detail in sale.Details)
            {
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

        var products = (await _productRepository.GetByCompanyIdAsync(companyId, ct))
            .ToDictionary(p => p.Id.Value, p => p);

        return accumulator
            .OrderByDescending(kvp => kvp.Value.Units)
            .Take(TopProductsCount)
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
        IReadOnlyCollection<Sale> sales, Domain.Companies.CompanyId companyId, CancellationToken ct)
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
