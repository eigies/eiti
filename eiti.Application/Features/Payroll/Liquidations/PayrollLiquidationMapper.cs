namespace eiti.Application.Features.Payroll.Liquidations;

internal static class PayrollLiquidationMapper
{
    public static PayrollLiquidationResponse Map(eiti.Domain.Payroll.PayrollLiquidation liquidation)
    {
        return new PayrollLiquidationResponse(
            liquidation.Id.Value,
            liquidation.EmployeeId.Value,
            liquidation.PeriodLabel,
            liquidation.GrossAmount,
            liquidation.NetAmount,
            (int)liquidation.Status,
            (int?)liquidation.PaymentMethod,
            liquidation.PaidAt,
            liquidation.DeductionLines.Select(l => new PayrollLiquidationLineResponse(l.ConceptName, l.Amount)).ToList(),
            liquidation.AdvanceLines.Select(l => new PayrollLiquidationLineResponse("Adelanto", l.Amount)).ToList());
    }
}
