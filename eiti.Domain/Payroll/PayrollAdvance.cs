using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;
using eiti.Domain.Users;

namespace eiti.Domain.Payroll;

public sealed class PayrollAdvance : AggregateRoot<PayrollAdvanceId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public EmployeeId EmployeeId { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public DateTime Date { get; private set; }
    public string? Notes { get; private set; }
    public PayrollAdvanceStatus Status { get; private set; }
    public PayrollLiquidationId? AppliedToLiquidationId { get; private set; }
    public UserId CreatedByUserId { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public Guid? CashSessionId { get; private set; }

    private PayrollAdvance()
    {
    }

    private PayrollAdvance(
        PayrollAdvanceId id,
        CompanyId companyId,
        EmployeeId employeeId,
        decimal amount,
        DateTime date,
        string? notes,
        UserId createdByUserId,
        Guid? cashSessionId = null)
        : base(id)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Advance amount must be greater than zero.", nameof(amount));
        }

        CompanyId = companyId;
        EmployeeId = employeeId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Date = date;
        Notes = NormalizeOptional(notes);
        Status = PayrollAdvanceStatus.Pending;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
        CashSessionId = cashSessionId;
    }

    public static PayrollAdvance Create(
        CompanyId companyId,
        EmployeeId employeeId,
        decimal amount,
        DateTime date,
        string? notes,
        UserId createdByUserId,
        Guid? cashSessionId = null)
    {
        return new PayrollAdvance(PayrollAdvanceId.New(), companyId, employeeId, amount, date, notes, createdByUserId, cashSessionId);
    }

    public void Cancel()
    {
        if (Status != PayrollAdvanceStatus.Pending)
        {
            throw new InvalidOperationException("Only pending advances can be cancelled.");
        }

        Status = PayrollAdvanceStatus.Cancelled;
    }

    public void Apply(PayrollLiquidationId liquidationId)
    {
        if (Status != PayrollAdvanceStatus.Pending)
        {
            throw new InvalidOperationException("Only pending advances can be applied.");
        }

        Status = PayrollAdvanceStatus.Applied;
        AppliedToLiquidationId = liquidationId;
    }

    public void Revert()
    {
        if (Status != PayrollAdvanceStatus.Applied)
        {
            throw new InvalidOperationException("Only applied advances can be reverted.");
        }

        Status = PayrollAdvanceStatus.Pending;
        AppliedToLiquidationId = null;
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
