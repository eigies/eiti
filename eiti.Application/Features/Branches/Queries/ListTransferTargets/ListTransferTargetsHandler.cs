using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Branches.Queries.ListTransferTargets;

public sealed class ListTransferTargetsHandler
    : IRequestHandler<ListTransferTargetsQuery, Result<IReadOnlyList<TransferTargetResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;

    public ListTransferTargetsHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
    }

    public async Task<Result<IReadOnlyList<TransferTargetResponse>>> Handle(
        ListTransferTargetsQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<TransferTargetResponse>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        // Destinos de traspaso = TODAS las sucursales de la empresa (solo id + nombre).
        // No se filtra por AllowedBranchIds: enviar stock a una sucursal no otorga visibilidad
        // de sus datos (las pantallas de lectura siguen gateadas por sucursal de forma independiente).
        var branches = await _branchRepository.ListByCompanyAsync(companyId, cancellationToken);

        var targets = branches
            .OrderBy(branch => branch.Name)
            .Select(branch => new TransferTargetResponse(branch.Id.Value, branch.Name))
            .ToList();

        return Result<IReadOnlyList<TransferTargetResponse>>.Success(targets);
    }
}
