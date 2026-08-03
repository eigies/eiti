namespace eiti.Application.Features.Reports.Queries.TradeInsByBranch;

// Operations = cantidad de ventas distintas que recibieron ese producto en canje.
// Amount = valor total reconocido por el canje (lo que se le descontó al cliente del total).
// AvgUnitValue = Amount / Units.
public sealed record TradeInsByBranchResponse(
    IReadOnlyList<TradeInsByBranchRow> Rows,
    TradeInsByBranchTotals Totals);

public sealed record TradeInsByBranchRow(
    Guid BranchId,
    string BranchName,
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    string ProductSku,
    int Operations,
    int Units,
    decimal Amount,
    decimal AvgUnitValue);

// Operations del total = ventas distintas con canje en todo el resultado (una venta que entregó
// dos productos distintos cuenta una sola vez, por eso no es la suma de la columna).
public sealed record TradeInsByBranchTotals(
    int Operations,
    int Units,
    decimal Amount,
    decimal AvgUnitValue);
