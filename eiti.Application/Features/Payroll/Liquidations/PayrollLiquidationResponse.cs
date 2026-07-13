namespace eiti.Application.Features.Payroll.Liquidations;

public sealed record PayrollLiquidationLineResponse(string Label, decimal Amount);

public sealed record PayrollLiquidationResponse(
    Guid Id,
    Guid EmployeeId,
    string PeriodLabel,
    decimal GrossAmount,
    decimal NetAmount,
    int Status,
    int? PaymentMethod,
    DateTime? PaidAt,
    IReadOnlyList<PayrollLiquidationLineResponse> DeductionLines,
    IReadOnlyList<PayrollLiquidationLineResponse> AdvanceLines,
    IReadOnlyList<PayrollLiquidationLineResponse> BonusLines);
