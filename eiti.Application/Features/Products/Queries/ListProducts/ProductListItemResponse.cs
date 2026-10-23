namespace eiti.Application.Features.Products.Queries.ListProducts;

public sealed record ProductListItemResponse(
    Guid Id,
    string Code,
    string Sku,
    string Brand,
    string Name,
    string? Description,
    decimal Price,
    decimal PublicPrice,
    // null = el usuario no tiene products.view_cost, no "costo cero". Enmascarar con 0m
    // hacia que ese 0 volviera al backend en el siguiente update y pisara el costo real.
    decimal? CostPrice,
    decimal? UnitPrice,
    bool AllowsManualValueInSale,
    decimal? NoDeliverySurcharge,
    Guid? CategoryId,
    string? CategoryName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int TotalOnHandQuantity,
    int TotalReservedQuantity,
    int TotalAvailableQuantity,
    int CompanyOnHandQuantity,
    int CompanyReservedQuantity,
    int CompanyAvailableQuantity);
