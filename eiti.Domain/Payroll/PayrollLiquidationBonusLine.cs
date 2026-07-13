namespace eiti.Domain.Payroll;

public sealed class PayrollLiquidationBonusLine
{
    public Guid Id { get; private set; }
    public PayrollLiquidationId PayrollLiquidationId { get; private set; } = null!;
    public Guid PayrollBonusId { get; private set; }
    public string ConceptName { get; private set; } = string.Empty;
    public PayrollBonusAmountType AmountType { get; private set; }
    public decimal Value { get; private set; }
    public decimal Amount { get; private set; }

    private PayrollLiquidationBonusLine()
    {
    }

    private PayrollLiquidationBonusLine(Guid id, Guid payrollBonusId, string conceptName, PayrollBonusAmountType amountType, decimal value, decimal amount)
    {
        Id = id;
        PayrollBonusId = payrollBonusId;
        ConceptName = conceptName.Trim();
        AmountType = amountType;
        Value = value;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static PayrollLiquidationBonusLine Create(Guid payrollBonusId, string conceptName, PayrollBonusAmountType amountType, decimal value, decimal amount)
    {
        return new PayrollLiquidationBonusLine(Guid.NewGuid(), payrollBonusId, conceptName, amountType, value, amount);
    }

    internal void AttachToLiquidation(PayrollLiquidationId liquidationId)
    {
        PayrollLiquidationId = liquidationId;
    }
}
