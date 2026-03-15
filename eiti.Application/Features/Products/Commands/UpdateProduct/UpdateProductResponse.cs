namespace eiti.Application.Features.Products.Commands.UpdateProduct;

public sealed record UpdateProductResponse(
    Guid Id,
    string Code,
    string Sku,
    string Brand,
    string Name,
    string? Description,
    decimal Price,
    decimal PublicPrice,
    decimal CostPrice,
    decimal? UnitPrice,
    bool AllowsManualValueInSale,
    decimal? NoDeliverySurcharge,
    int TotalOnHandQuantity,
    int TotalReservedQuantity,
    int TotalAvailableQuantity,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
