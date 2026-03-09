using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CreateCustomer;

public sealed record CreateCustomerCommand(
    string? Name,
    string? FirstName,
    string? LastName,
    string Email,
    string? Phone,
    int? DocumentType,
    string? DocumentNumber,
    string? TaxId,
    CreateCustomerAddressRequest? Address
) : IRequest<Result<CreateCustomerResponse>>;

public sealed record CreateCustomerAddressRequest(
    string Street,
    string StreetNumber,
    string PostalCode,
    string City,
    string StateOrProvince,
    string Country,
    string? Floor,
    string? Apartment,
    string? Reference);
