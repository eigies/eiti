using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.UpdateSupplier;

public sealed class UpdateSupplierHandler : IRequestHandler<UpdateSupplierCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSupplierHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateSupplierCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var supplier = await _supplierRepository.GetByIdAsync(command.Id, companyId.Value, cancellationToken);
        if (supplier is null)
            return Result.Failure(UpdateSupplierErrors.NotFound);

        supplier.Update(command.Name, command.Phone, command.Email, command.TaxId, command.Notes);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
