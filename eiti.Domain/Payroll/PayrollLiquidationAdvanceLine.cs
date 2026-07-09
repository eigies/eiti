namespace eiti.Domain.Payroll;

public sealed class PayrollLiquidationAdvanceLine
{
    public Guid Id { get; private set; }
    public Guid PayrollLiquidationId { get; private set; }
    public Guid PayrollAdvanceId { get; private set; }
    public decimal Amount { get; private set; }

    private PayrollLiquidationAdvanceLine()
    {
    }

    private PayrollLiquidationAdvanceLine(Guid id, Guid payrollAdvanceId, decimal amount)
    {
        Id = id;
        PayrollAdvanceId = payrollAdvanceId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static PayrollLiquidationAdvanceLine Create(Guid payrollAdvanceId, decimal amount)
    {
        return new PayrollLiquidationAdvanceLine(Guid.NewGuid(), payrollAdvanceId, amount);
    }

    internal void AttachToLiquidation(Guid liquidationId)
    {
        PayrollLiquidationId = liquidationId;
    }
}
