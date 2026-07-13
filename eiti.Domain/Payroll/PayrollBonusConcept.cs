using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Payroll;

public sealed class PayrollBonusConcept : AggregateRoot<PayrollBonusConceptId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PayrollBonusConcept()
    {
    }

    private PayrollBonusConcept(PayrollBonusConceptId id, CompanyId companyId, string name)
        : base(id)
    {
        CompanyId = companyId;
        Name = NormalizeName(name);
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public static PayrollBonusConcept Create(CompanyId companyId, string name)
    {
        return new PayrollBonusConcept(PayrollBonusConceptId.New(), companyId, name);
    }

    public void Update(string name)
    {
        Name = NormalizeName(name);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        var normalized = name.Trim();
        if (normalized.Length > 150)
        {
            throw new ArgumentException("Name cannot exceed 150 characters.", nameof(name));
        }

        return normalized;
    }
}
