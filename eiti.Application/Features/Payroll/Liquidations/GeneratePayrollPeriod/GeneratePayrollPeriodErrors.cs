using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;

public static class GeneratePayrollPeriodErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Liquidations.GeneratePayrollPeriod.Unauthorized",
        "Authentication is required.");
}
