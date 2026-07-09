namespace eiti.Domain.Payroll;

public sealed record PayrollLiquidationId(Guid Value)
{
    public static PayrollLiquidationId New() => new(Guid.NewGuid());
}
