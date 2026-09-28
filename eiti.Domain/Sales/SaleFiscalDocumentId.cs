namespace eiti.Domain.Sales;

public sealed record SaleFiscalDocumentId(Guid Value)
{
    public static SaleFiscalDocumentId New() => new(Guid.NewGuid());
}
