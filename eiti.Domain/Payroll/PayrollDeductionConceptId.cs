namespace eiti.Domain.Payroll;

public sealed record PayrollDeductionConceptId(Guid Value)
{
    public static PayrollDeductionConceptId New() => new(Guid.NewGuid());
}
