namespace eiti.Domain.Stock;

public sealed record BranchProductStockId(Guid Value)
{
    public static BranchProductStockId New() => new(Guid.NewGuid());
}
