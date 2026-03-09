using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Commands.CreateCustomer;
using eiti.Domain.Addresses;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.UpdateCustomer;

public sealed class UpdateCustomerHandler : IRequestHandler<UpdateCustomerCommand, Result<UpdateCustomerResponse>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAddressRepository _addressRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerHandler(
        ICustomerRepository customerRepository,
        IAddressRepository addressRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _addressRepository = addressRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UpdateCustomerResponse>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<UpdateCustomerResponse>.Failure(
                Error.Unauthorized("Customer.Update.Unauthorized", "El usuario actual no esta autenticado."));
        }

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (customer is null)
        {
            return Result<UpdateCustomerResponse>.Failure(
                Error.NotFound("Customer.Update.NotFound", "El cliente no fue encontrado."));
        }

        Email email;
        try
        {
            email = Email.Create(request.Email);
        }
        catch (ArgumentException ex)
        {
            return Result<UpdateCustomerResponse>.Failure(
                Error.Validation("Customer.Update.InvalidEmail", ex.Message));
        }

        if (customer.Email != email && await _customerRepository.EmailExistsAsync(email, _currentUserService.CompanyId, cancellationToken))
        {
            return Result<UpdateCustomerResponse>.Failure(
                Error.Conflict("Customer.Update.EmailExists", "Ya existe un cliente con ese email."));
        }

        var parsedNames = CreateCustomerHandler.ParseNames(request.Name, request.FirstName, request.LastName);
        if (parsedNames.IsFailure)
        {
            return Result<UpdateCustomerResponse>.Failure(parsedNames.Error);
        }

        DocumentType? documentType = null;
        if (request.DocumentType.HasValue)
        {
            if (!Enum.IsDefined(typeof(DocumentType), request.DocumentType.Value))
            {
                return Result<UpdateCustomerResponse>.Failure(
                    Error.Validation("Customer.Update.InvalidDocumentType", "El tipo de documento es invalido."));
            }

            documentType = (DocumentType)request.DocumentType.Value;
        }

        var normalizedDocument = CreateCustomerHandler.Normalize(request.DocumentNumber);
        var normalizedTaxId = CreateCustomerHandler.Normalize(request.TaxId);

        if (documentType.HasValue && !string.IsNullOrWhiteSpace(normalizedDocument))
        {
            var exists = await _customerRepository.DocumentExistsAsync(_currentUserService.CompanyId, documentType.Value, normalizedDocument, cancellationToken);
            if (exists && (customer.DocumentType != documentType || customer.DocumentNumber != normalizedDocument))
            {
                return Result<UpdateCustomerResponse>.Failure(
                    Error.Conflict("Customer.Update.DocumentExists", "Ya existe un cliente con ese documento."));
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedTaxId))
        {
            var exists = await _customerRepository.TaxIdExistsAsync(_currentUserService.CompanyId, normalizedTaxId, cancellationToken);
            if (exists && customer.TaxId != normalizedTaxId)
            {
                return Result<UpdateCustomerResponse>.Failure(
                    Error.Conflict("Customer.Update.TaxIdExists", "Ya existe un cliente con ese CUIT."));
            }
        }

        Address? address = null;
        if (request.Address is not null && CreateCustomerHandler.HasAddressData(request.Address))
        {
            if (customer.AddressId is not null)
            {
                address = await _addressRepository.GetByIdAsync(customer.AddressId, cancellationToken);
                if (address is not null)
                {
                    address.Update(
                        request.Address.Street,
                        request.Address.StreetNumber,
                        request.Address.PostalCode,
                        request.Address.City,
                        request.Address.StateOrProvince,
                        request.Address.Country,
                        request.Address.Floor,
                        request.Address.Apartment,
                        request.Address.Reference);
                }
            }

            if (address is null)
            {
                address = Address.Create(
                    request.Address.Street,
                    request.Address.StreetNumber,
                    request.Address.PostalCode,
                    request.Address.City,
                    request.Address.StateOrProvince,
                    request.Address.Country,
                    request.Address.Floor,
                    request.Address.Apartment,
                    request.Address.Reference);

                await _addressRepository.AddAsync(address, cancellationToken);
            }
        }

        try
        {
            customer.UpdateProfile(
                parsedNames.Value.firstName,
                parsedNames.Value.lastName,
                request.Phone,
                documentType,
                normalizedDocument,
                normalizedTaxId,
                address?.Id ?? customer.AddressId);
            customer.UpdateEmail(email);
        }
        catch (ArgumentException ex)
        {
            return Result<UpdateCustomerResponse>.Failure(
                Error.Validation("Customer.Update.InvalidInput", ex.Message));
        }

        _customerRepository.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UpdateCustomerResponse>.Success(
            new UpdateCustomerResponse(
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
