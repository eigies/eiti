using eiti.Application.Common;
using eiti.Application.Features.Customers.Commands.CreateCustomer;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.UpdateCustomer;

public sealed record UpdateCustomerCommand(
    Guid Id,
    string? Name,
    string? FirstName,
    string? LastName,
    string Email,
    string? Phone,
    int? DocumentType,
    string? DocumentNumber,
    string? TaxId,
    CreateCustomerAddressRequest? Address
) : IRequest<Result<UpdateCustomerResponse>>;
