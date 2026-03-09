namespace eiti.Domain.Cash;

public sealed record CashSessionId(Guid Value)
{
    public static CashSessionId New() => new(Guid.NewGuid());
}
