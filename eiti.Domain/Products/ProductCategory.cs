namespace eiti.Domain.Products;

public sealed class ProductCategory
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ProductCategory()
    {
    }

    public static ProductCategory Create(Guid companyId, string name)
    {
        return new ProductCategory
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = NormalizeName(name),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Rename(string name)
    {
        Name = NormalizeName(name);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Category name cannot be empty.", nameof(value));
        }

        var normalized = value.Trim();

        if (normalized.Length > 100)
        {
            throw new ArgumentException("Category name cannot exceed 100 characters.", nameof(value));
        }

        return normalized;
    }
}
