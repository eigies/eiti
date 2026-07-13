using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CancelPayrollBonus;

public static class CancelPayrollBonusErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Bonuses.Cancel.Unauthorized",
        "Authentication is required.");

    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Bonuses.Cancel.NotFound",
        "The requested bonus was not found.");
}
