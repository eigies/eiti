using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Products;

public sealed class Product : AggregateRoot<ProductId>
{
    public CompanyId CompanyId { get; private set; }
    public string Code { get; private set; }
    public string Sku { get; private set; }
    public string Brand { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public decimal CostPrice { get; private set; }
    public decimal? UnitPrice { get; private set; }
    public bool AllowsManualValueInSale { get; private set; }
    public decimal? NoDeliverySurcharge { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Product()
    {
    }

    private Product(
        ProductId id,
        CompanyId companyId,
        string code,
        string sku,
        string brand,
        string name,
        string? description,
        decimal publicPrice,
        decimal costPrice,
        decimal? unitPrice,
        bool allowsManualValueInSale,
        decimal? noDeliverySurcharge,
        DateTime createdAt)
        : base(id)
    {
        CompanyId = companyId;
        Code = code;
        Sku = sku;
        Brand = brand;
        Name = name;
        Description = description;
        Price = publicPrice;
        CostPrice = costPrice;
        UnitPrice = unitPrice;
        AllowsManualValueInSale = allowsManualValueInSale;
        NoDeliverySurcharge = noDeliverySurcharge;
        CreatedAt = createdAt;
    }

    public static Product Create(
        CompanyId companyId,
        string code,
        string sku,
        string brand,
        string name,
        string? description,
        decimal publicPrice,
        decimal costPrice,
        decimal? unitPrice,
        bool allowsManualValueInSale = false,
        decimal? noDeliverySurcharge = null)
    {
        var normalizedCode = NormalizeCode(code);
        var normalizedSku = NormalizeSku(sku);
        var normalizedBrand = NormalizeBrand(brand);
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);

        ValidatePricing(publicPrice, costPrice, unitPrice, allowsManualValueInSale);

        return new Product(
            ProductId.New(),
            companyId,
            normalizedCode,
            normalizedSku,
            normalizedBrand,
            normalizedName,
            normalizedDescription,
            publicPrice,
            costPrice,
            unitPrice,
            allowsManualValueInSale,
            noDeliverySurcharge,
            DateTime.UtcNow);
    }

    public void Update(
        string code,
        string sku,
        string brand,
        string name,
        string? description,
        decimal publicPrice,
        decimal costPrice,
        decimal? unitPrice,
        bool allowsManualValueInSale = false,
        decimal? noDeliverySurcharge = null)
    {
        var normalizedCode = NormalizeCode(code);
        var normalizedSku = NormalizeSku(sku);
        var normalizedBrand = NormalizeBrand(brand);
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);

        ValidatePricing(publicPrice, costPrice, unitPrice, allowsManualValueInSale);

        Code = normalizedCode;
        Sku = normalizedSku;
        Brand = normalizedBrand;
        Name = normalizedName;
        Description = normalizedDescription;
        Price = publicPrice;
        CostPrice = costPrice;
        UnitPrice = unitPrice;
        AllowsManualValueInSale = allowsManualValueInSale;
        NoDeliverySurcharge = noDeliverySurcharge;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidatePricing(
        decimal publicPrice,
        decimal costPrice,
        decimal? unitPrice,
        bool allowsManualValueInSale)
    {
        if (publicPrice < 0)
        {
            throw new ArgumentException("Public price cannot be negative.", nameof(publicPrice));
        }

        if (!allowsManualValueInSale && publicPrice <= 0)
        {
            throw new ArgumentException(
                "Public price must be greater than zero unless the product allows manual value in sale.",
                nameof(publicPrice));
        }

        if (costPrice < 0)
        {
            throw new ArgumentException("Cost price cannot be negative.", nameof(costPrice));
        }

        if (unitPrice.HasValue && unitPrice.Value < 0)
        {
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        }
    }

    private static string NormalizeBrand(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Product brand cannot be empty.", nameof(value));
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > 100)
        {
            throw new ArgumentException("Product brand cannot exceed 100 characters.", nameof(value));
        }

        return normalizedValue;
    }

    private static string NormalizeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Product code cannot be empty.", nameof(value));
        }

        var normalizedValue = value.Trim().ToUpperInvariant();

        if (normalizedValue.Length > 50)
        {
            throw new ArgumentException("Product code cannot exceed 50 characters.", nameof(value));
        }

        return normalizedValue;
    }

    private static string NormalizeSku(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Product SKU cannot be empty.", nameof(value));
        }

        var normalizedValue = value.Trim().ToUpperInvariant();

        if (normalizedValue.Length > 80)
        {
            throw new ArgumentException("Product SKU cannot exceed 80 characters.", nameof(value));
        }

        return normalizedValue;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Product name cannot be empty.", nameof(value));
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > 150)
        {
            throw new ArgumentException("Product name cannot exceed 150 characters.", nameof(value));
        }

        return normalizedValue;
    }

    private static string? NormalizeDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > 1000)
        {
            throw new ArgumentException("Product description cannot exceed 1000 characters.", nameof(value));
        }

        return normalizedValue;
    }
}
