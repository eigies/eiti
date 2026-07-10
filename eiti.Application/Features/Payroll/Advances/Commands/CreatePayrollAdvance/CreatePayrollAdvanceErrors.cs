using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;

public static class CreatePayrollAdvanceErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Advances.Create.Unauthorized",
        "Authentication is required.");

    public static readonly Error EmployeeNotFound = Error.NotFound(
        "Payroll.Advances.Create.EmployeeNotFound",
        "The requested employee was not found.");

    public static readonly Error CashSessionNotFound = Error.NotFound(
        "Payroll.Advances.Create.CashSessionNotFound",
        "The requested cash session was not found.");

    public static readonly Error CashSessionRequired = Error.Validation(
        "Payroll.Advances.Create.CashSessionRequired",
        "A cash session is required when paying in cash.");
}
