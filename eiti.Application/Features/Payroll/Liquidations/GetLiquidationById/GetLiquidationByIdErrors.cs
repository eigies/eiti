using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Liquidations.GetLiquidationById;

public static class GetLiquidationByIdErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Liquidations.GetById.NotFound",
        "The requested liquidation was not found.");

    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Liquidations.GetById.Unauthorized",
        "Authentication is required.");
}
