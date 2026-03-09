namespace eiti.Domain.Products;

public sealed record ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.NewGuid());

    public static ProductId Empty => new(Guid.Empty);
}
