using eiti.Application.Features.CashSessions.Common;

namespace eiti.Application.Features.Reports.Queries.CashSessionsReport;

public sealed record CashSessionsReportResponse(
    string DateFrom,
    string DateTo,
    IReadOnlyList<CashSessionsReportBranch> Branches);

public sealed record CashSessionsReportBranch(
    Guid BranchId,
    string BranchName,
    IReadOnlyList<CashSessionsReportDrawer> Drawers);

public sealed record CashSessionsReportDrawer(
    Guid CashDrawerId,
    string CashDrawerName,
    IReadOnlyList<CashSessionsReportSession> Sessions);

public sealed record CashSessionsReportSession(
    Guid SessionId,
    int Status,
    string StatusName,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal OpeningAmount,
    decimal ExpectedClosingAmount,
    decimal? ActualClosingAmount,
    decimal Difference,
    string? Notes,
    IReadOnlyList<CashSessionsReportTypeTotal> TotalsByType,
    IReadOnlyList<PaymentMethodBreakdownItem> PaymentBreakdown,
    IReadOnlyList<CashSessionsReportOtherMovement> OtherMovements);

public sealed record CashSessionsReportTypeTotal(
    int Type,
    string TypeName,
    int Count,
    decimal Total);

public sealed record CashSessionsReportOtherMovement(
    int Type,
    string TypeName,
    decimal Amount,
    DateTime OccurredAt,
    string Description,
    string? SaleCode,
    string? CreatedByUsername);
