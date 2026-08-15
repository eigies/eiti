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

        return Result<GetDashboardSummaryResponse>.Success(new GetDashboardSummaryResponse(
            month,
            today,
            Array.Empty<DashboardDayPoint>(),
            Array.Empty<DashboardTopProduct>(),
            new DashboardCollections(0m, 0, 0m, 0, 0m),
            new DashboardTodayStatus(todaySales.Count, 0, 0, 0),
            Array.Empty<DashboardRecentSale>()));
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
}
