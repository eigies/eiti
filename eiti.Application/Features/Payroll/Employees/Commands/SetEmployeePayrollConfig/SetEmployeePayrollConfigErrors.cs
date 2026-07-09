using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;

public static class SetEmployeePayrollConfigErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Employees.SetPayrollConfig.NotFound",
        "The requested employee was not found.");
}
