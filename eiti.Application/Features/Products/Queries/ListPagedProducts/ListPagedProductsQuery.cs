using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Products.Queries.ListPagedProducts;

public sealed record ListPagedProductsQuery(int Page = 1, int PageSize = 10, string? Query = null)
    : IRequest<Result<PagedProductsResponse>>;
