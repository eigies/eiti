using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Bonuses;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Queries.ListPayrollBonuses;

public sealed class ListPayrollBonusesHandler : IRequestHandler<ListPayrollBonusesQuery, Result<IReadOnlyList<PayrollBonusResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusRepository _repository;

    public ListPayrollBonusesHandler(ICurrentUserService currentUserService, IPayrollBonusRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<PayrollBonusResponse>>> Handle(ListPayrollBonusesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<PayrollBonusResponse>>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<IReadOnlyList<PayrollBonusResponse>>.Failure(ListPayrollBonusesErrors.Unauthorized);

        var employeeId = request.EmployeeId.HasValue ? new EmployeeId(request.EmployeeId.Value) : null;
        var status = request.Status.HasValue ? (PayrollBonusStatus)request.Status.Value : (PayrollBonusStatus?)null;

        var bonuses = await _repository.ListByCompanyAsync(_currentUserService.CompanyId, employeeId, status, cancellationToken);

        IReadOnlyList<PayrollBonusResponse> items = bonuses
            .Select(b => new PayrollBonusResponse(b.Id.Value, b.EmployeeId.Value, b.ConceptId.Value, (int)b.AmountType, b.Value, b.Notes, (int)b.Status, b.PayrollLiquidationId?.Value))
            .ToList();

        return Result<IReadOnlyList<PayrollBonusResponse>>.Success(items);
    }
}
