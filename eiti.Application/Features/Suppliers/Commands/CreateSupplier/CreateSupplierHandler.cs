using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Suppliers;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CreateSupplier;

public sealed class CreateSupplierHandler : IRequestHandler<CreateSupplierCommand, Result<CreateSupplierResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSupplierHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateSupplierResponse>> Handle(CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CreateSupplierResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var nameExists = await _supplierRepository.NameExistsAsync(companyId.Value, command.Name, cancellationToken);
        if (nameExists)
            return Result<CreateSupplierResponse>.Failure(CreateSupplierErrors.NameAlreadyExists);

        var supplier = Supplier.Create(
            companyId.Value,
            command.Name,
            command.Phone,
            command.Email,
            command.TaxId,
            command.Notes);

        await _supplierRepository.AddAsync(supplier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateSupplierResponse>.Success(new CreateSupplierResponse(supplier.Id, supplier.Name));
    }
}
