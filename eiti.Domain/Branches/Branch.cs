using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Branches;

public sealed class Branch : AggregateRoot<BranchId>
{
    public CompanyId CompanyId { get; private set; }
    public string Name { get; private set; }
    public string? Code { get; private set; }
    public string? Address { get; private set; }
    /// <summary>Override de <c>Company.AutomaticInvoicing</c>. Null = hereda de la empresa.</summary>
    public bool? AutomaticInvoicing { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Branch()
    {
    }

    private Branch(
        BranchId id,
        CompanyId companyId,
        string name,
        string? code,
        string? address,
        DateTime createdAt)
        : base(id)
    {
        CompanyId = companyId;
        Name = name;
        Code = code;
        Address = address;
        CreatedAt = createdAt;
    }

    public static Branch Create(
        CompanyId companyId,
        string name,
        string? code,
        string? address)
    {
        return new Branch(
            BranchId.New(),
            companyId,
            NormalizeRequired(name, 120, "Branch name"),
            NormalizeOptional(code, 40, "Branch code"),
            NormalizeOptional(address, 255, "Branch address"),
            DateTime.UtcNow);
    }

    public void Update(string name, string? code, string? address, bool? automaticInvoicing = null)
    {
        Name = NormalizeRequired(name, 120, "Branch name");
        Code = NormalizeOptional(code, 40, "Branch code");
        Address = NormalizeOptional(address, 255, "Branch address");
        AutomaticInvoicing = automaticInvoicing;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Resuelve la config efectiva: la sucursal manda si definió un valor, si no hereda de la empresa.</summary>
    public bool ResolveAutomaticInvoicing(bool companyDefault) => AutomaticInvoicing ?? companyDefault;

    private static string NormalizeRequired(string value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{field} cannot be empty.", nameof(value));
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }
}
