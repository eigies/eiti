using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Code,
    string Sku,
    string Brand,
    string Name,
    string? Description,
    decimal? Price,
    decimal? PublicPrice,
    decimal CostPrice,
    decimal? UnitPrice
) : IRequest<Result<CreateProductResponse>>;
