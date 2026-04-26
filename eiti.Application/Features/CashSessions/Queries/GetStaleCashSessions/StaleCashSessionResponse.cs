namespace eiti.Application.Features.CashSessions.Queries.GetStaleCashSessions;

public sealed record StaleCashSessionResponse(
    Guid SessionId,
    Guid CashDrawerId,
    DateTime OpenedAt,
    int HoursOpen);
