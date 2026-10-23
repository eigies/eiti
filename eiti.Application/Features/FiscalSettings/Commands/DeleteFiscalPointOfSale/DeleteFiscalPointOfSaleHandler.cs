using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.FiscalSettings.Common;
using eiti.Domain.Branches;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Commands.DeleteFiscalPointOfSale;

public sealed class DeleteFiscalPointOfSaleHandler : IRequestHandler<DeleteFiscalPointOfSaleCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IFiscalPointOfSaleRepository _pointsOfSale;
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFiscalPointOfSaleHandler(
        ICurrentUserService currentUserService,
        IFiscalPointOfSaleRepository pointsOfSale,
        IBranchRepository branchRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _pointsOfSale = pointsOfSale;
        _branchRepository = branchRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteFiscalPointOfSaleCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var pointOfSale = await _pointsOfSale.GetByIdAsync(new FiscalPointOfSaleId(request.PointOfSaleId), companyId, cancellationToken);
        if (pointOfSale is null)
            return Result.Failure(FiscalPointOfSaleErrors.NotFound);

        var branch = await _branchRepository.GetByFiscalPointOfSaleIdAsync(pointOfSale.Id, companyId, cancellationToken);
        if (branch is not null)
            return Result.Failure(FiscalPointOfSaleErrors.AssignedToBranch(branch.Name));

        // En el servicio queda habilitado: no molesta y las facturas ya emitidas lo siguen referenciando.
        _pointsOfSale.Remove(pointOfSale);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
