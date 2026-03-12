namespace eiti.Application.Features.Stock.Common;

public sealed record BranchProductStockResponse(
    Guid ProductId,
    Guid BranchId,
    string Code,
    string Sku,
    string Brand,
    string Name,
    decimal Price,
    decimal PublicPrice,
    decimal CostPrice,
    decimal? UnitPrice,
    bool AllowsManualValueInSale,
    int OnHandQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    DateTime? UpdatedAt);
