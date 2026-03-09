namespace eiti.Application.Features.Products.Queries.ListProducts;

public sealed record ProductListItemResponse(
    Guid Id,
    string Code,
    string Sku,
    string Brand,
    string Name,
    string? Description,
    decimal Price,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int TotalOnHandQuantity,
    int TotalReservedQuantity,
    int TotalAvailableQuantity);
