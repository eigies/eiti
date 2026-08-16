using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Dashboard.Queries.ListDashboardSales;

public sealed class ListDashboardSalesHandler
    : IRequestHandler<ListDashboardSalesQuery, Result<IReadOnlyList<DashboardSaleResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly TimeProvider _timeProvider;

    public ListDashboardSalesHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ICustomerRepository customerRepository,
        TimeProvider timeProvider)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IReadOnlyList<DashboardSaleResponse>>> Handle(
        ListDashboardSalesQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<DashboardSaleResponse>>.Failure(authCheck.Error);

        if (request.DateFrom.Date != request.DateTo.Date)
        {
            return Result<IReadOnlyList<DashboardSaleResponse>>.Failure(
                ListDashboardSalesErrors.SingleDayRequired);
        }

        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(
            _timeProvider.GetUtcNow().UtcDateTime,
            BusinessCalendar.TimeZone).Date;
        var requestedDay = request.DateFrom.Date;
        if (requestedDay < todayLocal.AddDays(-6) || requestedDay > todayLocal)
        {
            return Result<IReadOnlyList<DashboardSaleResponse>>.Failure(
                ListDashboardSalesErrors.DateOutsideWindow);
        }

        var canViewAllBranches = _currentUserService.CanViewAllBranches;
        if (request.BranchId.HasValue
            && !canViewAllBranches
            && !_currentUserService.AllowedBranchIds.Contains(request.BranchId.Value))
        {
            return Result<IReadOnlyList<DashboardSaleResponse>>.Failure(
                GetDashboardSummaryErrors.BranchNotAllowed);
        }

        var companyId = _currentUserService.CompanyId!;
        var allowedBranchIds = canViewAllBranches ? null : _currentUserService.AllowedBranchIds;
        var (from, to) = BusinessCalendar.ToUtcRange(request.DateFrom, request.DateTo);
        var sales = await _saleRepository.ListForDashboardAsync(
            companyId,
            from,
            to,
            request.BranchId,
            allowedBranchIds,
            cancellationToken);

        var customerIds = sales
            .Where(sale => sale.CustomerId is not null)
            .Select(sale => sale.CustomerId!)
            .Distinct()
            .ToList();
        var customerNames = customerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _customerRepository.ListByIdsAsync(companyId, customerIds, cancellationToken))
                .ToDictionary(customer => customer.Id.Value, customer => customer.FullName);
        var canViewFinancials = _currentUserService.HasPermission(
            PermissionCodes.DashboardViewFinancials);

        IReadOnlyList<DashboardSaleResponse> response = sales
            .Select(sale => new DashboardSaleResponse(
                sale.Id.Value,
                sale.Code,
                sale.CreatedAt,
                sale.CustomerId is not null
                    && customerNames.TryGetValue(sale.CustomerId.Value, out var name)
                        ? name
                        : "Consumidor final",
                (int)sale.SaleStatus,
                canViewFinancials ? sale.TotalAmount : 0m,
                sale.IsCuentaCorriente))
            .ToList();

        return Result<IReadOnlyList<DashboardSaleResponse>>.Success(response);
    }
}
