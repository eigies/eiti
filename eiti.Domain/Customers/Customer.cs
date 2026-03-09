using eiti.Domain.Addresses;
using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Customers;

public sealed class Customer : AggregateRoot<CustomerId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public string Phone { get; private set; } = string.Empty;
    public DocumentType? DocumentType { get; private set; }
    public string? DocumentNumber { get; private set; }
    public string? TaxId { get; private set; }
    public AddressId? AddressId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string FullName => string.IsNullOrWhiteSpace(LastName)
        ? FirstName
        : $"{FirstName} {LastName}".Trim();

    private Customer()
    {
    }

    private Customer(
        CustomerId id,
        CompanyId companyId,
        string firstName,
        string lastName,
        Email email,
        string? phone,
        DocumentType? documentType,
        string? documentNumber,
        string? taxId,
        AddressId? addressId,
        DateTime createdAt)
        : base(id)
    {
        CompanyId = companyId;
        FirstName = NormalizeRequired(firstName, nameof(firstName), 100);
        LastName = NormalizeOptional(lastName, 100) ?? string.Empty;
        Name = BuildDisplayName(FirstName, LastName);
        Email = email;
        Phone = NormalizeOptional(phone, 30) ?? string.Empty;
        DocumentType = documentType;
        DocumentNumber = NormalizeOptional(documentNumber, 30);
        TaxId = NormalizeOptional(taxId, 20);
        AddressId = addressId;
        CreatedAt = createdAt;
    }

    public static Customer Create(
        CompanyId companyId,
        string firstName,
        string lastName,
        Email email,
        string? phone = null,
        DocumentType? documentType = null,
        string? documentNumber = null,
        string? taxId = null,
        AddressId? addressId = null)
    {
        return new Customer(
            CustomerId.New(),
            companyId,
            firstName,
            lastName,
            email,
            phone,
            documentType,
            documentNumber,
            taxId,
            addressId,
            DateTime.UtcNow);
    }

    public void UpdateProfile(
        string firstName,
        string lastName,
        string? phone,
        DocumentType? documentType,
        string? documentNumber,
        string? taxId,
        AddressId? addressId)
    {
        FirstName = NormalizeRequired(firstName, nameof(firstName), 100);
        LastName = NormalizeOptional(lastName, 100) ?? string.Empty;
        Name = BuildDisplayName(FirstName, LastName);
        Phone = NormalizeOptional(phone, 30) ?? string.Empty;
        DocumentType = documentType;
        DocumentNumber = NormalizeOptional(documentNumber, 30);
        TaxId = NormalizeOptional(taxId, 20);
        AddressId = addressId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateEmail(Email newEmail)
    {
        Email = newEmail;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string BuildDisplayName(string firstName, string lastName)
    {
        return string.IsNullOrWhiteSpace(lastName)
            ? firstName
            : $"{firstName} {lastName}".Trim();
    }

    private static string NormalizeRequired(string value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{field} is required.", field);
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.", field);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"The value cannot exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }
}
