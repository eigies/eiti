namespace eiti.Domain.Sales;

public sealed record SaleId(Guid Value)
{
    public static SaleId New() => new(Guid.NewGuid());
}
