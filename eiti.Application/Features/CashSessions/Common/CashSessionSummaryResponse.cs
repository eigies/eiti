namespace eiti.Application.Features.CashSessions.Common;

public sealed record CashSessionSummaryResponse(
    Guid Id,
    decimal OpeningAmount,
    decimal SalesIncome,
    decimal Withdrawals,
    decimal ExpectedClosingAmount,
    decimal? ActualClosingAmount,
    decimal Difference);
