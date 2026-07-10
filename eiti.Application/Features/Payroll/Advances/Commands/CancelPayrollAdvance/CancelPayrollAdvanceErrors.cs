using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Advances.Commands.CancelPayrollAdvance;

public static class CancelPayrollAdvanceErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Advances.Cancel.NotFound",
        "The requested advance was not found.");

    public static readonly Error CashSessionNotFound = Error.NotFound(
        "Payroll.Advances.Cancel.CashSessionNotFound",
        "The original cash session was not found.");
}
