using eiti.Application.Features.Customers.Commands.CreateCustomer;

namespace eiti.Application.Features.Customers.Queries.GetCustomerById;

public sealed record GetCustomerByIdResponse(
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
