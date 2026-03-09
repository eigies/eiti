namespace eiti.Application.Features.CashSessions.Common;

public sealed record CashSessionMovementResponse(
    Guid Id,
    int Type,
    string TypeName,
    int Direction,
    string DirectionName,
    decimal Amount,
    DateTime OccurredAt,
    string Description,
    string? ReferenceType,
    Guid? ReferenceId);
