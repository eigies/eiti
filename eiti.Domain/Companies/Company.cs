using eiti.Domain.Primitives;

namespace eiti.Domain.Companies;

public sealed class Company : AggregateRoot<CompanyId>
{
    public CompanyName Name { get; private set; }
    public CompanyDomain PrimaryDomain { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Company()
    {
    }

    private Company(
        CompanyId id,
        CompanyName name,
        CompanyDomain primaryDomain,
        DateTime createdAt)
        : base(id)
    {
        Name = name;
        PrimaryDomain = primaryDomain;
        CreatedAt = createdAt;
    }

    public static Company Create(
        CompanyName name,
        CompanyDomain primaryDomain)
    {
        return new Company(
            CompanyId.New(),
            name,
            primaryDomain,
            DateTime.UtcNow);
    }

    public static Company CreateLegacy(CompanyId id)
    {
        return new Company(
            id,
            CompanyName.Create("Legacy Company"),
            CompanyDomain.Create("legacy.local"),
            DateTime.UtcNow);
    }

    public void Update(
        CompanyName name,
        CompanyDomain primaryDomain)
    {
        Name = name;
        PrimaryDomain = primaryDomain;
    }
}
