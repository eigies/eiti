using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Branches.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.Branches.Queries.ListBranches;

public sealed class ListBranchesHandler : IRequestHandler<ListBranchesQuery, Result<IReadOnlyList<BranchResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;

    public ListBranchesHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ISaleRepository saleRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _saleRepository = saleRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
    }

    public async Task<Result<IReadOnlyList<BranchResponse>>> Handle(ListBranchesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<BranchResponse>>.Failure(authCheck.Error);

        var branches = await _branchRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken);
        if (!_currentUserService.CanViewAllBranches)
        {
            var allowed = _currentUserService.AllowedBranchIds;
            branches = branches.Where(branch => allowed.Contains(branch.Id.Value)).ToList();
        }

        // Cifras de dinero/negocio (ventas y valor de caja) solo para quien puede ver montos.
        // Sin el permiso no se calculan ni se devuelven (no se filtra por la API).
        var canViewFinancials = _currentUserService.HasPermission(PermissionCodes.DashboardViewFinancials);

        var salesCountByBranch = new Dictionary<Guid, int>();
        if (canViewFinancials)
        {
            var sales = await _saleRepository.ListByCompanyAsync(_currentUserService.CompanyId, null, null, null, cancellationToken: cancellationToken);
            salesCountByBranch = sales
                .GroupBy(sale => sale.BranchId.Value)
                .ToDictionary(group => group.Key, group => group.Count());
        }

        var responses = new List<BranchResponse>(branches.Count);

        foreach (var branch in branches)
        {
            decimal cashValue = 0m;

            if (canViewFinancials)
            {
                var drawers = await _cashDrawerRepository.ListByBranchAsync(branch.Id, _currentUserService.CompanyId, cancellationToken);

                foreach (var drawer in drawers)
                {
                    var openSession = await _cashSessionRepository.GetOpenByDrawerAsync(drawer.Id, _currentUserService.CompanyId, cancellationToken);
                    if (openSession is not null && openSession.Status == CashSessionStatus.Open)
                    {
                        cashValue += openSession.ExpectedClosingAmount;
                    }
                }
            }

            responses.Add(new BranchResponse(
                branch.Id.Value,
                branch.Name,
                branch.Code,
                branch.Address,
                salesCountByBranch.GetValueOrDefault(branch.Id.Value, 0),
                cashValue,
                branch.CreatedAt,
                branch.UpdatedAt));
        }

        return Result<IReadOnlyList<BranchResponse>>.Success(responses);
    }
}
