namespace eiti.Application.Features.Customers.Commands.CreateCustomer;

public sealed record CreateCustomerResponse(
    Guid Id,
    string Name,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string Phone,
    int? DocumentType,
    string? DocumentTypeName,
    string? DocumentNumber,
    string? TaxId,
    Guid? AddressId,
    CustomerAddressResponse? Address,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CustomerAddressResponse(
    Guid Id,
    string Street,
    string StreetNumber,
    string? Floor,
    string? Apartment,
    string PostalCode,
    string City,
    string StateOrProvince,
    string Country,
    string? Reference,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
