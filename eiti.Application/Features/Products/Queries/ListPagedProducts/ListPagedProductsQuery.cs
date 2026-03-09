using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Products.Queries.ListPagedProducts;

public sealed record ListPagedProductsQuery(int Page = 1, int PageSize = 10)
    : IRequest<Result<PagedProductsResponse>>;
