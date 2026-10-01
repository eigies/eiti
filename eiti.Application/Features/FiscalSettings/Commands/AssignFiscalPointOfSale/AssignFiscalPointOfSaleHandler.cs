using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.FiscalSettings.Common;
using eiti.Domain.Branches;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Commands.AssignFiscalPointOfSale;

public sealed class AssignFiscalPointOfSaleHandler : IRequestHandler<AssignFiscalPointOfSaleCommand, Result<FiscalPointOfSaleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IFiscalPointOfSaleRepository _pointsOfSale;
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignFiscalPointOfSaleHandler(
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

    public async Task<Result<FiscalPointOfSaleResponse>> Handle(AssignFiscalPointOfSaleCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<FiscalPointOfSaleResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var pointOfSale = await _pointsOfSale.GetByIdAsync(new FiscalPointOfSaleId(request.PointOfSaleId), companyId, cancellationToken);
        if (pointOfSale is null)
            return Result<FiscalPointOfSaleResponse>.Failure(FiscalPointOfSaleErrors.NotFound);

        var assigned = await FiscalPointOfSaleAssignment.ApplyAsync(_branchRepository, pointOfSale, request.BranchId, companyId, cancellationToken);
        if (assigned.IsFailure)
            return Result<FiscalPointOfSaleResponse>.Failure(assigned.Error);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<FiscalPointOfSaleResponse>.Success(FiscalPointOfSaleResponse.From(pointOfSale, assigned.Value));
    }
}
