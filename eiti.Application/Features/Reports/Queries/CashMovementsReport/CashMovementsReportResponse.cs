namespace eiti.Application.Features.Reports.Queries.CashMovementsReport;

public sealed record CashMovementsReportResponse(
    IReadOnlyList<CashMovementCategory> Categories,
    decimal TotalGeneral);

public sealed record CashMovementCategory(
    string Motivo,
    int TypeCode,
    decimal Total,
    int Count,
    IReadOnlyList<CashMovementItem> Items);

public sealed record CashMovementItem(
    DateTime Date,
    string Description,
    decimal Amount,
    string? UserName);
