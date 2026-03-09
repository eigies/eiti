using eiti.Application.Features.Products.Queries.ListProducts;

namespace eiti.Application.Features.Products.Queries.ListPagedProducts;

public sealed record PagedProductsResponse(
    IReadOnlyList<ProductListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
