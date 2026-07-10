using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Employees;
using MediatR;

namespace eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;

public sealed class SetEmployeePayrollConfigHandler : IRequestHandler<SetEmployeePayrollConfigCommand, Result<SetEmployeePayrollConfigResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetEmployeePayrollConfigHandler(
        ICurrentUserService currentUserService,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SetEmployeePayrollConfigResponse>> Handle(SetEmployeePayrollConfigCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<SetEmployeePayrollConfigResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<SetEmployeePayrollConfigResponse>.Failure(SetEmployeePayrollConfigErrors.Unauthorized);

        var employee = await _employeeRepository.GetByIdAsync(new EmployeeId(request.EmployeeId), _currentUserService.CompanyId, cancellationToken);
        if (employee is null)
            return Result<SetEmployeePayrollConfigResponse>.Failure(SetEmployeePayrollConfigErrors.NotFound);

        PayrollPeriodicity? periodicity = null;
        if (request.PayrollPeriodicity.HasValue)
        {
            if (!Enum.IsDefined(typeof(PayrollPeriodicity), request.PayrollPeriodicity.Value))
                return Result<SetEmployeePayrollConfigResponse>.Failure(SetEmployeePayrollConfigErrors.InvalidPeriodicity);

            periodicity = (PayrollPeriodicity)request.PayrollPeriodicity.Value;
        }

        try
        {
            employee.SetPayrollConfig(request.BaseSalary, periodicity);
        }
        catch (ArgumentException ex)
        {
            return Result<SetEmployeePayrollConfigResponse>.Failure(Error.Validation("Payroll.Employees.SetPayrollConfig.InvalidInput", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SetEmployeePayrollConfigResponse>.Success(
            new SetEmployeePayrollConfigResponse(employee.Id.Value, employee.BaseSalary, (int?)employee.PayrollPeriodicity));
    }
}
