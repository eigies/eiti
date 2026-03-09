namespace eiti.Domain.Companies;

public sealed record CompanyId(Guid Value)
{
    public static CompanyId New() => new(Guid.NewGuid());

    public static CompanyId Empty => new(Guid.Empty);
}
