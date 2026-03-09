using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Products.Queries.ListProducts;

public sealed record ListProductsQuery()
    : IRequest<Result<IReadOnlyList<ProductListItemResponse>>>;
