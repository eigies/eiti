using eiti.Domain.Primitives;

namespace eiti.Domain.Addresses;

public sealed class Address : AggregateRoot<AddressId>
{
    public string Street { get; private set; } = string.Empty;
    public string StreetNumber { get; private set; } = string.Empty;
    public string? Floor { get; private set; }
    public string? Apartment { get; private set; }
    public string PostalCode { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string StateOrProvince { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string? Reference { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Address()
    {
    }

    private Address(
        AddressId id,
        string street,
        string streetNumber,
        string? floor,
        string? apartment,
        string postalCode,
        string city,
        string stateOrProvince,
        string country,
        string? reference,
        DateTime createdAt)
        : base(id)
    {
        Street = street;
        StreetNumber = streetNumber;
        Floor = floor;
        Apartment = apartment;
        PostalCode = postalCode;
        City = city;
        StateOrProvince = stateOrProvince;
        Country = country;
        Reference = reference;
        CreatedAt = createdAt;
    }

    public static Address Create(
        string street,
        string streetNumber,
        string postalCode,
        string city,
        string stateOrProvince,
        string country,
        string? floor = null,
        string? apartment = null,
        string? reference = null)
    {
        Validate(street, streetNumber, postalCode, city, stateOrProvince, country);

        return new Address(
            AddressId.New(),
            street.Trim(),
            streetNumber.Trim(),
            NormalizeOptional(floor),
            NormalizeOptional(apartment),
            postalCode.Trim(),
            city.Trim(),
            stateOrProvince.Trim(),
            country.Trim(),
            NormalizeOptional(reference),
            DateTime.UtcNow);
    }

    public void Update(
        string street,
        string streetNumber,
        string postalCode,
        string city,
        string stateOrProvince,
        string country,
        string? floor = null,
        string? apartment = null,
        string? reference = null)
    {
        Validate(street, streetNumber, postalCode, city, stateOrProvince, country);

        Street = street.Trim();
        StreetNumber = streetNumber.Trim();
        Floor = NormalizeOptional(floor);
        Apartment = NormalizeOptional(apartment);
        PostalCode = postalCode.Trim();
        City = city.Trim();
        StateOrProvince = stateOrProvince.Trim();
        Country = country.Trim();
        Reference = NormalizeOptional(reference);
        UpdatedAt = DateTime.UtcNow;
    }

    private static void Validate(
        string street,
        string streetNumber,
        string postalCode,
        string city,
        string stateOrProvince,
        string country)
    {
        Require(street, nameof(street), 120);
        Require(streetNumber, nameof(streetNumber), 20);
        Require(postalCode, nameof(postalCode), 20);
        Require(city, nameof(city), 100);
        Require(stateOrProvince, nameof(stateOrProvince), 100);
        Require(country, nameof(country), 100);
    }

    private static void Require(string value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{field} is required.", field);
        }

        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.", field);
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
