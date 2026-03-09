namespace eiti.Application.Features.Stock.Common;

public sealed record BranchProductStockResponse(
    Guid ProductId,
    Guid BranchId,
    string Code,
    string Sku,
    string Brand,
    string Name,
    decimal Price,
    int OnHandQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    DateTime? UpdatedAt);
