using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Products.Queries.ListProducts;

public sealed class ListProductsHandler
    : IRequestHandler<ListProductsQuery, Result<IReadOnlyList<ProductListItemResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;

    public ListProductsHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
    }

    public async Task<Result<IReadOnlyList<ProductListItemResponse>>> Handle(
        ListProductsQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<ProductListItemResponse>>.Failure(authCheck.Error);

        var products = await _productRepository.GetByCompanyIdAsync(
            _currentUserService.CompanyId,
            cancellationToken);

        var stocks = await _branchProductStockRepository.ListByCompanyAsync(
            _currentUserService.CompanyId,
            cancellationToken);

        var stockTotals = stocks
            .GroupBy(stock => stock.ProductId.Value)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    OnHand = group.Sum(item => item.OnHandQuantity),
                    Reserved = group.Sum(item => item.ReservedQuantity),
                    Available = group.Sum(item => item.AvailableQuantity)
                });

        return Result<IReadOnlyList<ProductListItemResponse>>.Success(
            products
            .Select(product =>
            {
                stockTotals.TryGetValue(product.Id.Value, out var totals);
                return new ProductListItemResponse(
                    product.Id.Value,
                    product.Code,
                    product.Sku,
                    product.Brand,
                    product.Name,
                    product.Description,
                    product.Price,
                    product.Price,
                    product.CostPrice,
                    product.UnitPrice,
                    product.AllowsManualValueInSale,
                    product.NoDeliverySurcharge,
                    product.CreatedAt,
                    product.UpdatedAt,
                    totals?.OnHand ?? 0,
                    totals?.Reserved ?? 0,
                    totals?.Available ?? 0);
            })
            .ToList());
    }
}
