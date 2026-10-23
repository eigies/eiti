namespace eiti.Application.Features.Branches.Common;

public sealed record BranchResponse(
    Guid Id,
    string Name,
    string? Code,
    string? Address,
    bool? AutomaticInvoicing,
    int SalesCount,
    decimal CashValue,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Punto de venta de ARCA donde factura la sucursal (1:1). Null = todavía no puede facturar.
    int? FiscalPointOfSaleNumber = null,
    Guid? FiscalPointOfSaleId = null);
