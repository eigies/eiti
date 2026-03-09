namespace eiti.Domain.Branches;

public sealed record BranchId(Guid Value)
{
    public static BranchId New() => new(Guid.NewGuid());
}
