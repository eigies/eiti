using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Liquidations;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.ListLiquidations;

public sealed class ListLiquidationsHandler : IRequestHandler<ListLiquidationsQuery, Result<ListLiquidationsResponse>>
{
    private const int MaxPageSize = 200;

    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollLiquidationRepository _repository;

    public ListLiquidationsHandler(ICurrentUserService currentUserService, IPayrollLiquidationRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<ListLiquidationsResponse>> Handle(ListLiquidationsQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<ListLiquidationsResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<ListLiquidationsResponse>.Failure(ListLiquidationsErrors.Unauthorized);

        var companyId = _currentUserService.CompanyId!;
        var employeeId = request.EmployeeId.HasValue ? new EmployeeId(request.EmployeeId.Value) : null;
        var status = request.Status.HasValue ? (PayrollLiquidationStatus)request.Status.Value : (PayrollLiquidationStatus?)null;
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 25 : Math.Min(request.PageSize, MaxPageSize);

        var totalCount = await _repository.CountAsync(companyId, employeeId, request.PeriodLabel, status, cancellationToken);
        var liquidations = await _repository.ListAsync(companyId, employeeId, request.PeriodLabel, status, page, pageSize, cancellationToken);

        var items = liquidations.Select(PayrollLiquidationMapper.Map).ToList();
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<ListLiquidationsResponse>.Success(new ListLiquidationsResponse(items, page, pageSize, totalCount, totalPages));
    }
}
