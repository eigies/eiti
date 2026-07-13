using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;

namespace eiti.Domain.Payroll;

public sealed class PayrollBonus : AggregateRoot<PayrollBonusId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public EmployeeId EmployeeId { get; private set; } = null!;
    public PayrollBonusConceptId ConceptId { get; private set; } = null!;
    public PayrollBonusAmountType AmountType { get; private set; }
    public decimal Value { get; private set; }
    public string? Notes { get; private set; }
    public PayrollBonusStatus Status { get; private set; }
    public PayrollLiquidationId? PayrollLiquidationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PayrollBonus()
    {
    }

    private PayrollBonus(
        PayrollBonusId id,
        CompanyId companyId,
        EmployeeId employeeId,
        PayrollBonusConceptId conceptId,
        PayrollBonusAmountType amountType,
        decimal value,
        string? notes)
        : base(id)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Bonus value must be greater than zero.", nameof(value));
        }

        if (amountType == PayrollBonusAmountType.Percentage && value > 100)
        {
            throw new ArgumentException("Percentage value must be between 0 and 100.", nameof(value));
        }

        CompanyId = companyId;
        EmployeeId = employeeId;
        ConceptId = conceptId;
        AmountType = amountType;
        Value = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        Notes = NormalizeOptional(notes);
        Status = PayrollBonusStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public static PayrollBonus Create(
        CompanyId companyId,
        EmployeeId employeeId,
        PayrollBonusConceptId conceptId,
        PayrollBonusAmountType amountType,
        decimal value,
        string? notes)
    {
        return new PayrollBonus(PayrollBonusId.New(), companyId, employeeId, conceptId, amountType, value, notes);
    }

    public decimal Resolve(decimal employeeBaseSalary) =>
        AmountType == PayrollBonusAmountType.FixedAmount
            ? Value
            : decimal.Round(employeeBaseSalary * Value / 100m, 2, MidpointRounding.AwayFromZero);

    public void Apply(PayrollLiquidationId liquidationId)
    {
        if (Status != PayrollBonusStatus.Pending)
        {
            throw new InvalidOperationException("Only pending bonuses can be applied.");
        }

        Status = PayrollBonusStatus.Applied;
        PayrollLiquidationId = liquidationId;
    }

    public void Cancel()
    {
        if (Status != PayrollBonusStatus.Pending)
        {
            throw new InvalidOperationException("Only pending bonuses can be cancelled.");
        }

        Status = PayrollBonusStatus.Cancelled;
    }

    public void RevertToPending()
    {
        if (Status != PayrollBonusStatus.Applied)
        {
            throw new InvalidOperationException("Only applied bonuses can be reverted.");
        }

        Status = PayrollBonusStatus.Pending;
        PayrollLiquidationId = null;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > 500)
        {
            throw new ArgumentException("Notes cannot exceed 500 characters.", nameof(value));
        }

        return normalized;
    }
}
