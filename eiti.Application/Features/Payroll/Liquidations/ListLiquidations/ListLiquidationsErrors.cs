using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Liquidations.ListLiquidations;

public static class ListLiquidationsErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Liquidations.List.Unauthorized",
        "Authentication is required.");
}
