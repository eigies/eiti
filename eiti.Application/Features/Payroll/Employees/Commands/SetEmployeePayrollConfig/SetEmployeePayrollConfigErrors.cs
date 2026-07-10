using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;

public static class SetEmployeePayrollConfigErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Employees.SetPayrollConfig.NotFound",
        "The requested employee was not found.");

    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Employees.SetPayrollConfig.Unauthorized",
        "Authentication is required.");

    public static readonly Error InvalidPeriodicity = Error.Validation(
        "Payroll.Employees.SetPayrollConfig.InvalidPeriodicity",
        "The selected payroll periodicity is invalid.");
}
