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
    // Nullable por simetria con UpdateProductCommand: quien no ve el costo manda null.
    // En un alta no hay costo previo que preservar, asi que null se persiste como 0.
    decimal? CostPrice,
    decimal? UnitPrice,
    bool AllowsManualValueInSale = false,
    decimal? NoDeliverySurcharge = null,
    Guid? CategoryId = null
) : IRequest<Result<CreateProductResponse>>;
