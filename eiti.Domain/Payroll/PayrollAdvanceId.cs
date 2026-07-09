namespace eiti.Domain.Payroll;

public sealed record PayrollAdvanceId(Guid Value)
{
    public static PayrollAdvanceId New() => new(Guid.NewGuid());
}
