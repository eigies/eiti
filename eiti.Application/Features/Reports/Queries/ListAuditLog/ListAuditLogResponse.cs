namespace eiti.Application.Features.Reports.Queries.ListAuditLog;

public sealed record ListAuditLogResponse(
    IReadOnlyList<AuditLogItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record AuditLogItemResponse(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string ActionType,
    bool Succeeded,
    string? ErrorCode,
    string? PayloadJson,
    DateTime Timestamp);
