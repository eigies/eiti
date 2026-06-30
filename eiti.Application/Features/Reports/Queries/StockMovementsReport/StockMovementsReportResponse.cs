namespace eiti.Application.Features.Reports.Queries.StockMovementsReport;

public sealed record StockMovementsReportResponse(
    IReadOnlyList<StockMovementsReportRow> Rows,
    StockMovementsReportTotals Totals,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record StockMovementsReportRow(
    DateTime Date,
    Guid BranchId,
    string BranchName,
    Guid ProductId,
    string Code,
    string Brand,
    string Name,
    int TypeId,
    string TypeName,
    int Direction,          // +1 entrada, -1 salida, 0 neutro (reserva/ajuste)
    int Quantity,           // siempre positiva
    string? ReferenceType,
    Guid? ReferenceId,      // para abrir el detalle (ej. traspaso)
    string? ReferenceCode,
    string? Description);

public sealed record StockMovementsReportTotals(
    int Entradas,
    int Salidas,
    int Neto,
    int Count);
