using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashDrawers.Common;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashDrawers.Queries.ListCashDrawers;

public sealed class ListCashDrawersHandler : IRequestHandler<ListCashDrawersQuery, Result<IReadOnlyList<CashDrawerResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;

    public ListCashDrawersHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
    }

    public async Task<Result<IReadOnlyList<CashDrawerResponse>>> Handle(ListCashDrawersQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<CashDrawerResponse>>.Failure(authCheck.Error);

        var branchAccess = _currentUserService.EnsureBranchAccess(request.BranchId);
        if (branchAccess.IsFailure)
            return Result<IReadOnlyList<CashDrawerResponse>>.Failure(branchAccess.Error);

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), _currentUserService.CompanyId, cancellationToken);

        if (branch is null)
        {
            return Result<IReadOnlyList<CashDrawerResponse>>.Failure(Error.NotFound("CashDrawers.List.BranchNotFound", "The requested branch was not found."));
        }

        var drawers = await _cashDrawerRepository.ListByBranchAsync(branch.Id, _currentUserService.CompanyId, cancellationToken);
        var assignedDrawer = await CashDrawerAccessPolicy.GetRestrictedAssignedDrawerAsync(
            _currentUserService,
            _cashDrawerRepository,
            cancellationToken);

        if (assignedDrawer is not null && !_currentUserService.HasPermission(PermissionCodes.CashDrawerManage))
        {
            drawers = drawers.Where(drawer => drawer.Id == assignedDrawer.Id).ToList();
        }

        var openDrawerIds = await _cashSessionRepository.GetOpenDrawerIdsAsync(
            drawers.Select(d => d.Id.Value),
            cancellationToken);
        var assignedUserIdsByDrawerId = await _cashDrawerRepository.GetAssignedUserIdsByDrawerIdsAsync(
            drawers.Select(drawer => drawer.Id.Value),
            cancellationToken);

        return Result<IReadOnlyList<CashDrawerResponse>>.Success(
            drawers.Select(drawer =>
            {
                var assignedUserIds = assignedUserIdsByDrawerId.TryGetValue(drawer.Id.Value, out var ids)
                    ? ids
                    : Array.Empty<Guid>();

                return new CashDrawerResponse(
                    drawer.Id.Value,
                    drawer.BranchId.Value,
                    drawer.Name,
                    drawer.IsActive,
                    drawer.CreatedAt,
                    drawer.UpdatedAt,
                    assignedUserIds.Count > 0 ? assignedUserIds[0] : null,
                    assignedUserIds,
                    openDrawerIds.Contains(drawer.Id.Value));
            }).ToList());
    }
}
