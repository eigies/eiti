namespace eiti.Domain.Payroll;

public sealed class PayrollLiquidationDeductionLine
{
    public Guid Id { get; private set; }
    public PayrollLiquidationId PayrollLiquidationId { get; private set; } = null!;
    public string ConceptName { get; private set; } = string.Empty;
    public decimal Percentage { get; private set; }
    public decimal Amount { get; private set; }

    private PayrollLiquidationDeductionLine()
    {
    }

    private PayrollLiquidationDeductionLine(Guid id, string conceptName, decimal percentage, decimal amount)
    {
        Id = id;
        ConceptName = conceptName.Trim();
        Percentage = percentage;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static PayrollLiquidationDeductionLine Create(string conceptName, decimal percentage, decimal amount)
    {
        return new PayrollLiquidationDeductionLine(Guid.NewGuid(), conceptName, percentage, amount);
    }

    internal void AttachToLiquidation(PayrollLiquidationId liquidationId)
    {
        PayrollLiquidationId = liquidationId;
    }
}
