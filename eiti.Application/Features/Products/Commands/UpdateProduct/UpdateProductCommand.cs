using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    string Code,
    string Sku,
    string Brand,
    string Name,
    string? Description,
    decimal? Price,
    decimal? PublicPrice,
    // Nullable a proposito: null significa "no tocar el costo actual", no "costo cero".
    // Las pantallas que no muestran el costo (sin products.view_cost) mandan null y el
    // costo vigente queda intacto. Un 0 explicito sigue siendo un costo cero valido.
    decimal? CostPrice,
    decimal? UnitPrice,
    bool AllowsManualValueInSale = false,
    decimal? NoDeliverySurcharge = null,
    Guid? CategoryId = null
) : IRequest<Result<UpdateProductResponse>>;
