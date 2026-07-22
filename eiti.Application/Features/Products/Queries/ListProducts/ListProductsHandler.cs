using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Products.Queries.ListProducts;

public sealed class ListProductsHandler
    : IRequestHandler<ListProductsQuery, Result<IReadOnlyList<ProductListItemResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;
    private readonly IProductCategoryRepository _categoryRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;

    public ListProductsHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        IProductCategoryRepository categoryRepository,
        IBranchProductStockRepository branchProductStockRepository)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _branchProductStockRepository = branchProductStockRepository;
    }

    public async Task<Result<IReadOnlyList<ProductListItemResponse>>> Handle(
        ListProductsQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<ProductListItemResponse>>.Failure(authCheck.Error);

        // Sin el permiso de costo no se expone el costo (se devuelve 0), para que no viaje en el JSON.
        var canViewCost = _currentUserService.HasPermission(PermissionCodes.ProductsViewCost);

        var products = await _productRepository.GetByCompanyIdAsync(
            _currentUserService.CompanyId,
            cancellationToken);

        var stocks = await _branchProductStockRepository.ListByCompanyAsync(
            _currentUserService.CompanyId,
            cancellationToken);

        var categories = (await _categoryRepository.ListByCompanyAsync(
                _currentUserService.CompanyId!.Value, cancellationToken) ?? [])
            .ToDictionary(category => category.Id, category => category.Name);

        // El total "global" se acota a las sucursales visibles del usuario; el total de empresa
        // (todas las sucursales) se expone aparte para el chip informativo.
        var canViewAll = _currentUserService.CanViewAllBranches;
        var allowed = _currentUserService.AllowedBranchIds;

        var stockTotals = stocks
            .GroupBy(stock => stock.ProductId.Value)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var visible = (canViewAll
                        ? group
                        : group.Where(item => allowed.Contains(item.BranchId.Value))).ToList();
                    return new
                    {
                        OnHand = visible.Sum(item => item.OnHandQuantity),
                        Reserved = visible.Sum(item => item.ReservedQuantity),
                        Available = visible.Sum(item => item.AvailableQuantity),
                        CompanyOnHand = group.Sum(item => item.OnHandQuantity),
                        CompanyReserved = group.Sum(item => item.ReservedQuantity),
                        CompanyAvailable = group.Sum(item => item.AvailableQuantity)
                    };
                });

        return Result<IReadOnlyList<ProductListItemResponse>>.Success(
            products
            .Select(product =>
            {
                stockTotals.TryGetValue(product.Id.Value, out var totals);
                var categoryName = product.CategoryId.HasValue && categories.TryGetValue(product.CategoryId.Value, out var name)
                    ? name
                    : null;
                return new ProductListItemResponse(
                    product.Id.Value,
                    product.Code,
                    product.Sku,
                    product.Brand,
                    product.Name,
                    product.Description,
                    product.Price,
                    product.Price,
                    canViewCost ? product.CostPrice : 0m,
                    product.UnitPrice,
                    product.AllowsManualValueInSale,
                    product.NoDeliverySurcharge,
                    product.CategoryId,
                    categoryName,
                    product.CreatedAt,
                    product.UpdatedAt,
                    totals?.OnHand ?? 0,
                    totals?.Reserved ?? 0,
                    totals?.Available ?? 0,
                    totals?.CompanyOnHand ?? 0,
                    totals?.CompanyReserved ?? 0,
                    totals?.CompanyAvailable ?? 0);
            })
            .ToList());
    }
}
