namespace eiti.Application.Features.Stock.Common;

public sealed record StockMovementResponse(
    Guid Id,
    int Type,
    string TypeName,
    int Quantity,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Description,
    DateTime CreatedAt,
    // Código legible del documento que originó el movimiento (venta/compra). Null si no aplica.
    string? ReferenceCode);
