using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Bonuses.Queries.ListPayrollBonuses;

public static class ListPayrollBonusesErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Bonuses.List.Unauthorized",
        "Authentication is required.");
}
