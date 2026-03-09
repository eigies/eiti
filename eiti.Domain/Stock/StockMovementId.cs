namespace eiti.Domain.Stock;

public sealed record StockMovementId(Guid Value)
{
    public static StockMovementId New() => new(Guid.NewGuid());
}
