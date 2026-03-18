using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Addresses;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, Result<CreateCustomerResponse>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAddressRepository _addressRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerHandler(
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

    public async Task<Result<CreateCustomerResponse>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CreateCustomerResponse>.Failure(authCheck.Error);

        Email email;

        try
        {
            email = Email.Create(request.Email);
        }
        catch (ArgumentException ex)
        {
            return Result<CreateCustomerResponse>.Failure(
                Error.Validation("Customer.Create.InvalidEmail", ex.Message));
        }

        if (await _customerRepository.EmailExistsAsync(email, _currentUserService.CompanyId, cancellationToken))
        {
            return Result<CreateCustomerResponse>.Failure(CreateCustomerErrors.EmailAlreadyExists);
        }

        var parsedNames = ParseNames(request.Name, request.FirstName, request.LastName);
        if (!parsedNames.IsSuccess)
        {
            return Result<CreateCustomerResponse>.Failure(parsedNames.Error);
        }

        DocumentType? documentType = null;
        if (request.DocumentType.HasValue)
        {
            if (!Enum.IsDefined(typeof(DocumentType), request.DocumentType.Value))
            {
                return Result<CreateCustomerResponse>.Failure(
                    Error.Validation("Customer.Create.InvalidDocumentType", "El tipo de documento es invalido."));
            }

            documentType = (DocumentType)request.DocumentType.Value;
        }

        var normalizedDocument = Normalize(request.DocumentNumber);
        var normalizedTaxId = Normalize(request.TaxId);

        if (documentType.HasValue && !string.IsNullOrWhiteSpace(normalizedDocument))
        {
            if (await _customerRepository.DocumentExistsAsync(_currentUserService.CompanyId, documentType.Value, normalizedDocument, cancellationToken))
            {
                return Result<CreateCustomerResponse>.Failure(
                    Error.Conflict("Customer.Create.DocumentExists", "Ya existe un cliente con ese documento."));
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedTaxId))
        {
            if (await _customerRepository.TaxIdExistsAsync(_currentUserService.CompanyId, normalizedTaxId, cancellationToken))
            {
                return Result<CreateCustomerResponse>.Failure(
                    Error.Conflict("Customer.Create.TaxIdExists", "Ya existe un cliente con ese CUIT."));
            }
        }

        Address? address = null;
        if (request.Address is not null && HasAddressData(request.Address))
        {
            try
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
            }
            catch (ArgumentException ex)
            {
                return Result<CreateCustomerResponse>.Failure(
                    Error.Validation("Customer.Create.InvalidAddress", ex.Message));
            }

            await _addressRepository.AddAsync(address, cancellationToken);
        }

        Customer customer;

        try
        {
            customer = Customer.Create(
                _currentUserService.CompanyId,
                parsedNames.Value.firstName,
                parsedNames.Value.lastName,
                email,
                request.Phone,
                documentType,
                normalizedDocument,
                normalizedTaxId,
                address?.Id);
        }
        catch (ArgumentException ex)
        {
            return Result<CreateCustomerResponse>.Failure(
                Error.Validation("Customer.Create.InvalidInput", ex.Message));
        }

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateCustomerResponse>.Success(ToResponse(customer, address));
    }

    internal static Result<(string firstName, string lastName)> ParseNames(string? legacyName, string? firstName, string? lastName)
    {
        var normalizedFirstName = Normalize(firstName);
        var normalizedLastName = Normalize(lastName) ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(normalizedFirstName))
        {
            return Result<(string firstName, string lastName)>.Success((normalizedFirstName, normalizedLastName));
        }

        var normalizedLegacyName = Normalize(legacyName);
        if (string.IsNullOrWhiteSpace(normalizedLegacyName))
        {
            return Result<(string firstName, string lastName)>.Failure(
                Error.Validation("Customer.Create.NameRequired", "Debe informar un nombre."));
        }

        var parts = normalizedLegacyName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var first = parts[0];
        var last = parts.Length > 1 ? parts[1] : string.Empty;

        return Result<(string firstName, string lastName)>.Success((first, last));
    }

    internal static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static bool HasAddressData(CreateCustomerAddressRequest address)
    {
        return !string.IsNullOrWhiteSpace(address.Street)
            || !string.IsNullOrWhiteSpace(address.City)
            || !string.IsNullOrWhiteSpace(address.PostalCode);
    }

    internal static CreateCustomerResponse ToResponse(Customer customer, Address? address)
    {
        return new CreateCustomerResponse(
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
            customer.UpdatedAt);
    }
}
