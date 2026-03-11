using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Stock.Common;
using eiti.Domain.Branches;
using MediatR;

namespace eiti.Application.Features.Stock.Queries.ListBranchStock;

public sealed class ListBranchStockHandler : IRequestHandler<ListBranchStockQuery, Result<IReadOnlyList<BranchProductStockResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;

    public ListBranchStockHandler(
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

    public async Task<Result<IReadOnlyList<BranchProductStockResponse>>> Handle(ListBranchStockQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<IReadOnlyList<BranchProductStockResponse>>.Failure(
                Error.Unauthorized("Stock.List.Unauthorized", "The current user is not authenticated."));
        }

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), _currentUserService.CompanyId, cancellationToken);
        if (branch is null)
        {
            return Result<IReadOnlyList<BranchProductStockResponse>>.Failure(
                Error.NotFound("Stock.List.BranchNotFound", "The selected branch was not found."));
        }

        var products = await _productRepository.GetByCompanyIdAsync(_currentUserService.CompanyId, cancellationToken);
        var stocks = await _branchProductStockRepository.ListByBranchAsync(branch.Id, _currentUserService.CompanyId, cancellationToken);
        var stockMap = stocks.ToDictionary(stock => stock.ProductId.Value);

        return Result<IReadOnlyList<BranchProductStockResponse>>.Success(
            products.Select(product =>
            {
                stockMap.TryGetValue(product.Id.Value, out var stock);
                return new BranchProductStockResponse(
                    product.Id.Value,
                    branch.Id.Value,
                    product.Code,
                    product.Sku,
                    product.Brand,
                    product.Name,
                    product.Price,
                    product.Price,
                    product.CostPrice,
                    product.UnitPrice,
                    stock?.OnHandQuantity ?? 0,
                    stock?.ReservedQuantity ?? 0,
                    stock?.AvailableQuantity ?? 0,
                    stock?.UpdatedAt);
            }).ToList());
    }
}
