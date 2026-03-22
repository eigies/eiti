namespace eiti.Domain.Sales;

public sealed record SaleCcPaymentId(Guid Value)
{
    public static SaleCcPaymentId New() => new(Guid.NewGuid());
}
