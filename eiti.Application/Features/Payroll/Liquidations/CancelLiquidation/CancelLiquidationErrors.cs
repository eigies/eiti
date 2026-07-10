using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Liquidations.CancelLiquidation;

public static class CancelLiquidationErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Liquidations.Cancel.NotFound",
        "The requested liquidation was not found.");
}
