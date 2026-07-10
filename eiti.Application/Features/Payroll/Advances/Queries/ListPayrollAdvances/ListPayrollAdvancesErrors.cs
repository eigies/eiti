using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Advances.Queries.ListPayrollAdvances;

public static class ListPayrollAdvancesErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Advances.List.Unauthorized",
        "Authentication is required.");
}
