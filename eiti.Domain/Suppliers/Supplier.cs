namespace eiti.Domain.Suppliers;

public sealed class Supplier
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? TaxId { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Supplier()
    {
    }

    public static Supplier Create(
        Guid companyId,
        string name,
        string? phone,
        string? email,
        string? taxId,
        string? notes)
    {
        return new Supplier
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = NormalizeRequired(name, 200, nameof(name)),
            Phone = NormalizeOptional(phone, 50),
            Email = NormalizeOptional(email, 200),
            TaxId = NormalizeOptional(taxId, 50),
            Notes = NormalizeOptional(notes, 500),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? phone, string? email, string? taxId, string? notes)
    {
        Name = NormalizeRequired(name, 200, nameof(name));
        Phone = NormalizeOptional(phone, 50);
        Email = NormalizeOptional(email, 200);
        TaxId = NormalizeOptional(taxId, 50);
        Notes = NormalizeOptional(notes, 500);
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static string NormalizeRequired(string value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException($"The value cannot exceed {maxLength} characters.", paramName);

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException($"The value cannot exceed {maxLength} characters.");

        return normalized;
    }
}
