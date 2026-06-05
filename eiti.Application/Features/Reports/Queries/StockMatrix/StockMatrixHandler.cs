using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.StockMatrix;

public sealed class StockMatrixHandler : IRequestHandler<StockMatrixQuery, Result<StockMatrixResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;

    public StockMatrixHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
    }

    public async Task<Result<StockMatrixResponse>> Handle(StockMatrixQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<StockMatrixResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;

        // Columnas: solo las sucursales visibles del usuario.
        var branches = (await _branchRepository.ListByCompanyAsync(companyId, cancellationToken)).ToList();
        if (!_currentUserService.CanViewAllBranches)
        {
            var allowed = _currentUserService.AllowedBranchIds;
            branches = branches.Where(branch => allowed.Contains(branch.Id.Value)).ToList();
        }

        var orderedBranches = branches
            .OrderBy(branch => branch.Name)
            .ToList();
        var branchIndex = orderedBranches
            .Select((branch, index) => (branch.Id.Value, index))
            .ToDictionary(x => x.Item1, x => x.index);

        var products = await _productRepository.GetByCompanyIdAsync(companyId, cancellationToken);
        var stocks = await _branchProductStockRepository.ListByCompanyAsync(companyId, cancellationToken);

        // (productId, branchId) -> disponible, solo de sucursales visibles
        var availableByProductBranch = stocks
            .Where(stock => branchIndex.ContainsKey(stock.BranchId.Value))
            .GroupBy(stock => stock.ProductId.Value)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(s => s.BranchId.Value, s => s.AvailableQuantity));

        var rows = products
            .Select(product =>
            {
                var cells = new int[orderedBranches.Count];
                if (availableByProductBranch.TryGetValue(product.Id.Value, out var byBranch))
                {
                    foreach (var (branchId, available) in byBranch)
                    {
                        if (branchIndex.TryGetValue(branchId, out var idx))
                            cells[idx] = available;
                    }
                }

                return new StockMatrixRow(
                    product.Id.Value,
                    product.Code,
                    product.Brand,
                    product.Name,
                    cells,
                    cells.Sum());
            })
            .OrderByDescending(row => row.Total)
            .ThenBy(row => row.Brand)
            .ThenBy(row => row.Name)
            .ToList();

        return Result<StockMatrixResponse>.Success(
            new StockMatrixResponse(
                orderedBranches.Select(branch => new StockMatrixBranch(branch.Id.Value, branch.Name)).ToList(),
                rows));
    }
}
