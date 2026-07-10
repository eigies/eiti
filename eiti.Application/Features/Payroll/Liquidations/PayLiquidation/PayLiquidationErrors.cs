using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Liquidations.PayLiquidation;

public static class PayLiquidationErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.Liquidations.Pay.NotFound",
        "The requested liquidation was not found.");

    public static readonly Error CashSessionNotFound = Error.NotFound(
        "Payroll.Liquidations.Pay.CashSessionNotFound",
        "The requested cash session was not found.");

    public static readonly Error CashSessionRequired = Error.Validation(
        "Payroll.Liquidations.Pay.CashSessionRequired",
        "A cash session is required when paying in cash.");
}
