namespace eiti.Domain.Employees;

public sealed record EmployeeId(Guid Value)
{
    public static EmployeeId New() => new(Guid.NewGuid());
}
