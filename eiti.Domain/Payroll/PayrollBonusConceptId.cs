namespace eiti.Domain.Payroll;

public sealed record PayrollBonusConceptId(Guid Value)
{
    public static PayrollBonusConceptId New() => new(Guid.NewGuid());
}
