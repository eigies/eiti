using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.FiscalSettings.Common;
using eiti.Domain.Branches;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Commands.CreateFiscalPointOfSale;

public sealed class CreateFiscalPointOfSaleHandler : IRequestHandler<CreateFiscalPointOfSaleCommand, Result<FiscalPointOfSaleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IFiscalPointOfSaleRepository _pointsOfSale;
    private readonly IBranchRepository _branchRepository;
    private readonly IFiscalizationService _fiscalizationService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFiscalPointOfSaleHandler(
        ICurrentUserService currentUserService,
        IFiscalPointOfSaleRepository pointsOfSale,
        IBranchRepository branchRepository,
        IFiscalizationService fiscalizationService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _pointsOfSale = pointsOfSale;
        _branchRepository = branchRepository;
        _fiscalizationService = fiscalizationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FiscalPointOfSaleResponse>> Handle(CreateFiscalPointOfSaleCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<FiscalPointOfSaleResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        if (request.Number < 1 || request.Number > FiscalPointOfSale.MaxNumber)
            return Result<FiscalPointOfSaleResponse>.Failure(FiscalPointOfSaleErrors.OutOfRange);

        if (await _pointsOfSale.NumberInUseAsync(companyId, request.Number, null, cancellationToken))
            return Result<FiscalPointOfSaleResponse>.Failure(FiscalPointOfSaleErrors.InUse(request.Number));

        var pointOfSale = FiscalPointOfSale.Create(companyId, request.Number);
        // La sucursal se valida antes de tocar el servicio, para no habilitar un número que no se guarda.
        var assigned = await FiscalPointOfSaleAssignment.ApplyAsync(_branchRepository, pointOfSale, request.BranchId, companyId, cancellationToken);
        if (assigned.IsFailure)
            return Result<FiscalPointOfSaleResponse>.Failure(assigned.Error);

        // Se habilita en el perfil del servicio primero: si no lo acepta, no se guarda nada y la
        // sucursal no queda con un punto de venta que rechace todas las facturas.
        if (_fiscalizationService.IsEnabled)
        {
            var registered = await _fiscalizationService.RegisterPointOfSaleAsync(companyId.Value, request.Number, cancellationToken);
            if (!registered.IsSuccess)
                return Result<FiscalPointOfSaleResponse>.Failure(FiscalPointOfSaleErrors.NotRegistered(registered.ErrorMessage));
        }

        await _pointsOfSale.AddAsync(pointOfSale, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<FiscalPointOfSaleResponse>.Success(FiscalPointOfSaleResponse.From(pointOfSale, assigned.Value));
    }
}
