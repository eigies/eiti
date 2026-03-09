namespace eiti.Domain.Cash;

public sealed record CashDrawerId(Guid Value)
{
    public static CashDrawerId New() => new(Guid.NewGuid());
}
