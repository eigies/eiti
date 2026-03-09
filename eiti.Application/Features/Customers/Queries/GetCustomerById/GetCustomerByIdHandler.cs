using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Commands.CreateCustomer;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.GetCustomerById;

public sealed class GetCustomerByIdHandler : IRequestHandler<GetCustomerByIdQuery, Result<GetCustomerByIdResponse>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAddressRepository _addressRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetCustomerByIdHandler(ICustomerRepository customerRepository, IAddressRepository addressRepository, ICurrentUserService currentUserService)
    {
        _customerRepository = customerRepository;
        _addressRepository = addressRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<GetCustomerByIdResponse>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<GetCustomerByIdResponse>.Failure(
                Error.Unauthorized("Customer.GetById.Unauthorized", "El usuario actual no esta autenticado."));
        }

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(request.Id), _currentUserService.CompanyId, cancellationToken);

        if (customer is null)
        {
            return Result<GetCustomerByIdResponse>.Failure(GetCustomerByIdErrors.NotFound);
        }

        var address = customer.AddressId is null
            ? null
            : await _addressRepository.GetByIdAsync(customer.AddressId, cancellationToken);

        return Result<GetCustomerByIdResponse>.Success(
            new GetCustomerByIdResponse(
                customer.Id.Value,
                customer.Name,
                customer.FirstName,
                customer.LastName,
                customer.FullName,
                customer.Email.Value,
                customer.Phone,
                customer.DocumentType.HasValue ? (int)customer.DocumentType.Value : null,
                customer.DocumentType?.ToString(),
                customer.DocumentNumber,
                customer.TaxId,
                customer.AddressId?.Value,
                address is null
                    ? null
                    : new CustomerAddressResponse(
                        address.Id.Value,
                        address.Street,
                        address.StreetNumber,
                        address.Floor,
                        address.Apartment,
                        address.PostalCode,
                        address.City,
                        address.StateOrProvince,
                        address.Country,
                        address.Reference,
                        address.CreatedAt,
                        address.UpdatedAt),
                customer.CreatedAt,
                customer.UpdatedAt));
    }
}
