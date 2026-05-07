using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Cash;

public sealed class CashDrawer : AggregateRoot<CashDrawerId>
{
    public CompanyId CompanyId { get; private set; }
    public BranchId BranchId { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private CashDrawer()
    {
    }

    private CashDrawer(
        CashDrawerId id,
        CompanyId companyId,
        BranchId branchId,
        string name,
        DateTime createdAt)
        : base(id)
    {
        CompanyId = companyId;
        BranchId = branchId;
        Name = name;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public static CashDrawer Create(
        CompanyId companyId,
        BranchId branchId,
        string name)
    {
        return new CashDrawer(
            CashDrawerId.New(),
            companyId,
            branchId,
            NormalizeName(name),
            DateTime.UtcNow);
    }

    public void Update(string name, bool isActive)
    {
        Name = NormalizeName(name);
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Cash drawer name cannot be empty.", nameof(value));
        }

        var normalized = value.Trim();

        if (normalized.Length > 120)
        {
            throw new ArgumentException("Cash drawer name cannot exceed 120 characters.", nameof(value));
        }

        return normalized;
    }
}
