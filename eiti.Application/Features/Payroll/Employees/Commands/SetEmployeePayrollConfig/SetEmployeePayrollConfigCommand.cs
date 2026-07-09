using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;

public sealed record SetEmployeePayrollConfigCommand(Guid EmployeeId, decimal? BaseSalary, int? PayrollPeriodicity)
    : IRequest<Result<SetEmployeePayrollConfigResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
