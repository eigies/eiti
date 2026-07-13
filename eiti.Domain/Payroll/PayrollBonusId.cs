namespace eiti.Domain.Payroll;

public sealed record PayrollBonusId(Guid Value)
{
    public static PayrollBonusId New() => new(Guid.NewGuid());
}
