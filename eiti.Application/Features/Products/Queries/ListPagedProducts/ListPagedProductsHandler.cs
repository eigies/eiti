using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Products.Queries.ListProducts;
using MediatR;

namespace eiti.Application.Features.Products.Queries.ListPagedProducts;

public sealed class ListPagedProductsHandler
    : IRequestHandler<ListPagedProductsQuery, Result<PagedProductsResponse>>
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;

    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;

    public ListPagedProductsHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
    }

    public async Task<Result<PagedProductsResponse>> Handle(
        ListPagedProductsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<PagedProductsResponse>.Failure(
                Error.Unauthorized(
                    "Products.ListPaged.Unauthorized",
                    "The current user is not authenticated."));
        }

        var pageSize = request.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(request.PageSize, MaxPageSize);

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

        var items = products
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
                    product.CreatedAt,
                    product.UpdatedAt,
                    totals?.OnHand ?? 0,
                    totals?.Reserved ?? 0,
                    totals?.Available ?? 0);
            })
            .ToList();

        var totalCount = items.Count;
        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)pageSize);
        var page = request.Page <= 0
            ? DefaultPage
            : Math.Min(request.Page, totalPages);

        var pagedItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result<PagedProductsResponse>.Success(
            new PagedProductsResponse(
                pagedItems,
                page,
                pageSize,
                totalCount,
                totalPages));
    }
}
