using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Advances;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Queries.ListPayrollAdvances;

public sealed class ListPayrollAdvancesHandler : IRequestHandler<ListPayrollAdvancesQuery, Result<IReadOnlyList<PayrollAdvanceResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollAdvanceRepository _repository;

    public ListPayrollAdvancesHandler(ICurrentUserService currentUserService, IPayrollAdvanceRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<PayrollAdvanceResponse>>> Handle(ListPayrollAdvancesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<PayrollAdvanceResponse>>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<IReadOnlyList<PayrollAdvanceResponse>>.Failure(ListPayrollAdvancesErrors.Unauthorized);

        var employeeId = request.EmployeeId.HasValue ? new EmployeeId(request.EmployeeId.Value) : null;
        var status = request.Status.HasValue ? (PayrollAdvanceStatus)request.Status.Value : (PayrollAdvanceStatus?)null;

        var advances = await _repository.ListByCompanyAsync(_currentUserService.CompanyId, employeeId, status, cancellationToken);

        IReadOnlyList<PayrollAdvanceResponse> items = advances
            .Select(a => new PayrollAdvanceResponse(a.Id.Value, a.EmployeeId.Value, a.Amount, a.Date, a.Notes, (int)a.Status, a.AppliedToLiquidationId?.Value, a.CashSessionId))
            .ToList();

        return Result<IReadOnlyList<PayrollAdvanceResponse>>.Success(items);
    }
}
