using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;

namespace eiti.Domain.Payroll;

public sealed class PayrollLiquidation : AggregateRoot<PayrollLiquidationId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public EmployeeId EmployeeId { get; private set; } = null!;
    public BranchId? BranchId { get; private set; }
    public string PeriodLabel { get; private set; } = string.Empty;
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public decimal GrossAmount { get; private set; }
    public PayrollLiquidationStatus Status { get; private set; }
    public PayrollPaymentMethod? PaymentMethod { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public Guid? CashSessionId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<PayrollLiquidationDeductionLine> _deductionLines = [];
    private readonly List<PayrollLiquidationAdvanceLine> _advanceLines = [];
    public IReadOnlyCollection<PayrollLiquidationDeductionLine> DeductionLines => _deductionLines;
    public IReadOnlyCollection<PayrollLiquidationAdvanceLine> AdvanceLines => _advanceLines;

    public decimal NetAmount => GrossAmount - _deductionLines.Sum(l => l.Amount) - _advanceLines.Sum(l => l.Amount);

    private PayrollLiquidation()
    {
    }

    private PayrollLiquidation(
        PayrollLiquidationId id,
        CompanyId companyId,
        EmployeeId employeeId,
        BranchId? branchId,
        string periodLabel,
        DateTime periodStart,
        DateTime periodEnd,
        decimal grossAmount,
        IReadOnlyList<PayrollLiquidationDeductionLine> deductionLines,
        IReadOnlyList<PayrollLiquidationAdvanceLine> advanceLines)
        : base(id)
    {
        if (grossAmount <= 0)
        {
            throw new ArgumentException("Gross amount must be greater than zero.", nameof(grossAmount));
        }

        if (string.IsNullOrWhiteSpace(periodLabel))
        {
            throw new ArgumentException("Period label cannot be empty.", nameof(periodLabel));
        }

        CompanyId = companyId;
        EmployeeId = employeeId;
        BranchId = branchId;
        PeriodLabel = periodLabel.Trim();
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        GrossAmount = decimal.Round(grossAmount, 2, MidpointRounding.AwayFromZero);
        Status = PayrollLiquidationStatus.Pending;
        CreatedAt = DateTime.UtcNow;

        foreach (var line in deductionLines)
        {
            line.AttachToLiquidation(Id);
            _deductionLines.Add(line);
        }

        foreach (var line in advanceLines)
        {
            line.AttachToLiquidation(Id);
            _advanceLines.Add(line);
        }
    }

    public static PayrollLiquidation Create(
        CompanyId companyId,
        EmployeeId employeeId,
        BranchId? branchId,
        string periodLabel,
        DateTime periodStart,
        DateTime periodEnd,
        decimal grossAmount,
        IReadOnlyList<PayrollLiquidationDeductionLine> deductionLines,
        IReadOnlyList<PayrollLiquidationAdvanceLine> advanceLines)
    {
        return new PayrollLiquidation(
            PayrollLiquidationId.New(),
            companyId,
            employeeId,
            branchId,
            periodLabel,
            periodStart,
            periodEnd,
            grossAmount,
            deductionLines,
            advanceLines);
    }

    public void MarkAsPaid(PayrollPaymentMethod method, Guid? cashSessionId)
    {
        if (Status != PayrollLiquidationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending liquidations can be marked as paid.");
        }

        if (method == PayrollPaymentMethod.Cash && !cashSessionId.HasValue)
        {
            throw new ArgumentException("A cash session is required when paying in cash.", nameof(cashSessionId));
        }

        PaymentMethod = method;
        CashSessionId = method == PayrollPaymentMethod.Cash ? cashSessionId : null;
        Status = PayrollLiquidationStatus.Paid;
        PaidAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == PayrollLiquidationStatus.Cancelled)
        {
            throw new InvalidOperationException("Liquidation is already cancelled.");
        }

        Status = PayrollLiquidationStatus.Cancelled;
    }
}
