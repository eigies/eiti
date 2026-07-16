namespace eiti.Domain.Quotes;

public sealed record QuoteId(Guid Value)
{
    public static QuoteId New() => new(Guid.NewGuid());
}
