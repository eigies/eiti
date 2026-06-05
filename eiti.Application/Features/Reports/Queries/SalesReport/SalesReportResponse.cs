namespace eiti.Application.Features.Reports.Queries.SalesReport;

// SalesCount = cantidad de ventas (tickets) distintas; ver nota en el reporte del front.
public sealed record SalesReportResponse(
    string GroupBy,
    IReadOnlyList<SalesReportRow> Rows,
    SalesReportTotals Totals);

public sealed record SalesReportRow(
    string Key,
    string Label,
    string? SubKey,
    string? SubLabel,
    int SalesCount,
    int Units,
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal MarginPct);

public sealed record SalesReportTotals(
    int SalesCount,
    int Units,
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal MarginPct);
