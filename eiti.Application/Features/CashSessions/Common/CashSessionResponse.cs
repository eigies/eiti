namespace eiti.Application.Features.CashSessions.Common;

public sealed record CashSessionResponse(
    Guid Id,
    Guid BranchId,
    Guid CashDrawerId,
    int Status,
    string StatusName,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal OpeningAmount,
    decimal ExpectedClosingAmount,
    decimal? ActualClosingAmount,
    decimal Difference,
    string? Notes,
    IReadOnlyList<CashSessionMovementResponse> Movements);
