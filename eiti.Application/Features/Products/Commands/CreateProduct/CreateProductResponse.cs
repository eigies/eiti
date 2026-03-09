namespace eiti.Application.Features.Products.Commands.CreateProduct;

public sealed record CreateProductResponse(
    Guid Id,
    string Code,
    string Sku,
    string Brand,
    string Name,
    string? Description,
    decimal Price,
    int TotalOnHandQuantity,
    int TotalReservedQuantity,
    int TotalAvailableQuantity,
    DateTime CreatedAt);
