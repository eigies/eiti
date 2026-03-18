using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.CashDrawers.Common;
using eiti.Domain.Branches;
using MediatR;

namespace eiti.Application.Features.CashDrawers.Queries.ListCashDrawers;

public sealed class ListCashDrawersHandler : IRequestHandler<ListCashDrawersQuery, Result<IReadOnlyList<CashDrawerResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;

    public ListCashDrawersHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ICashDrawerRepository cashDrawerRepository)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _cashDrawerRepository = cashDrawerRepository;
    }

    public async Task<Result<IReadOnlyList<CashDrawerResponse>>> Handle(ListCashDrawersQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<CashDrawerResponse>>.Failure(authCheck.Error);

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), _currentUserService.CompanyId, cancellationToken);

        if (branch is null)
        {
            return Result<IReadOnlyList<CashDrawerResponse>>.Failure(Error.NotFound("CashDrawers.List.BranchNotFound", "The requested branch was not found."));
        }

        var drawers = await _cashDrawerRepository.ListByBranchAsync(branch.Id, _currentUserService.CompanyId, cancellationToken);

        return Result<IReadOnlyList<CashDrawerResponse>>.Success(
            drawers.Select(drawer => new CashDrawerResponse(drawer.Id.Value, drawer.BranchId.Value, drawer.Name, drawer.IsActive, drawer.CreatedAt, drawer.UpdatedAt)).ToList());
    }
}
