namespace eiti.Application.Features.Reports.Queries.SalesReport;

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
