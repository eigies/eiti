using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Payroll;

public sealed class PayrollDeductionConcept : AggregateRoot<PayrollDeductionConceptId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public decimal Percentage { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PayrollDeductionConcept()
    {
    }

    private PayrollDeductionConcept(PayrollDeductionConceptId id, CompanyId companyId, string name, decimal percentage)
        : base(id)
    {
        CompanyId = companyId;
        Name = NormalizeName(name);
        Percentage = NormalizePercentage(percentage);
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public static PayrollDeductionConcept Create(CompanyId companyId, string name, decimal percentage)
    {
        return new PayrollDeductionConcept(PayrollDeductionConceptId.New(), companyId, name, percentage);
    }

    public void Update(string name, decimal percentage)
    {
        Name = NormalizeName(name);
        Percentage = NormalizePercentage(percentage);
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

    private static decimal NormalizePercentage(decimal percentage)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentException("Percentage must be between 0 and 100.", nameof(percentage));
        }

        return decimal.Round(percentage, 2, MidpointRounding.AwayFromZero);
    }
}
