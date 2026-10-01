using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Branches.Common;
using eiti.Domain.Branches;
using MediatR;

namespace eiti.Application.Features.Branches.Commands.SetBranchPointOfSale;

public sealed class SetBranchPointOfSaleHandler : IRequestHandler<SetBranchPointOfSaleCommand, Result<BranchResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly IFiscalPointOfSaleRepository _pointsOfSale;
    private readonly IFiscalizationService _fiscalizationService;
    private readonly IUnitOfWork _unitOfWork;

    public SetBranchPointOfSaleHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        IFiscalPointOfSaleRepository pointsOfSale,
        IFiscalizationService fiscalizationService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _pointsOfSale = pointsOfSale;
        _fiscalizationService = fiscalizationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BranchResponse>> Handle(SetBranchPointOfSaleCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BranchResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), companyId, cancellationToken);
        if (branch is null)
        {
            return Result<BranchResponse>.Failure(SetBranchPointOfSaleErrors.NotFound);
        }

        var applied = await ApplyAsync(branch, request.Number, cancellationToken);
        if (applied.IsFailure)
        {
            return Result<BranchResponse>.Failure(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BranchResponse>.Success(new(branch.Id.Value, branch.Name, branch.Code, branch.Address, branch.AutomaticInvoicing, 0, 0m,
            branch.CreatedAt, branch.UpdatedAt, branch.FiscalPointOfSale?.Number));
    }

    /// <summary>
    /// Un número nuevo se habilita antes en el perfil fiscal del servicio: si el servicio no lo
    /// acepta no se guarda nada, así nunca queda una sucursal con un punto de venta que después
    /// rechace todas las facturas.
    /// </summary>
    private async Task<Result> ApplyAsync(Branch branch, int? number, CancellationToken cancellationToken)
    {
        var current = branch.FiscalPointOfSale;
        if (number is null)
        {
            if (current is not null)
            {
                branch.ClearFiscalPointOfSale();
                _pointsOfSale.Remove(current);
            }

            return Result.Success();
        }

        if (number < 1 || number > FiscalPointOfSale.MaxNumber)
        {
            return Result.Failure(SetBranchPointOfSaleErrors.OutOfRange);
        }

        if (current?.Number == number)
        {
            return Result.Success();
        }

        if (await _pointsOfSale.NumberInUseAsync(branch.CompanyId, number.Value, current?.Id, cancellationToken))
        {
            return Result.Failure(SetBranchPointOfSaleErrors.InUse(number.Value));
        }

        if (_fiscalizationService.IsEnabled)
        {
            var registered = await _fiscalizationService.RegisterPointOfSaleAsync(branch.CompanyId.Value, number.Value, cancellationToken);
            if (!registered.IsSuccess)
            {
                return Result.Failure(SetBranchPointOfSaleErrors.NotRegistered(registered.ErrorMessage));
            }
        }

        if (current is not null)
        {
            current.ChangeNumber(number.Value);
            return Result.Success();
        }

        var pointOfSale = FiscalPointOfSale.Create(branch.CompanyId, number.Value);
        await _pointsOfSale.AddAsync(pointOfSale, cancellationToken);
        branch.AssignFiscalPointOfSale(pointOfSale);
        return Result.Success();
    }
}
