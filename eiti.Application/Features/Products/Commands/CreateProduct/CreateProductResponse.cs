namespace eiti.Application.Features.Products.Commands.CreateProduct;

public sealed record CreateProductResponse(
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
    DateTime CreatedAt);
