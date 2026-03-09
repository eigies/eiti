namespace eiti.Domain.Cash;

public sealed record CashMovementId(Guid Value)
{
    public static CashMovementId New() => new(Guid.NewGuid());
}
