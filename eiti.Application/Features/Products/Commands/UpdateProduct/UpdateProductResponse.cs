namespace eiti.Application.Features.Products.Commands.UpdateProduct;

public sealed record UpdateProductResponse(
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
    DateTime CreatedAt,
    DateTime? UpdatedAt);
