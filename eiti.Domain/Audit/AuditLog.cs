using eiti.Domain.Companies;
using eiti.Domain.Primitives;
using eiti.Domain.Users;

namespace eiti.Domain.Audit;

public sealed class AuditLog : Entity<AuditLogId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public UserId? UserId { get; private set; }
    public string ActionType { get; private set; } = string.Empty;
    public bool Succeeded { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? PayloadJson { get; private set; }
    public DateTime Timestamp { get; private set; }

    private AuditLog()
    {
    }

    private AuditLog(
        AuditLogId id,
        CompanyId companyId,
        UserId? userId,
        string actionType,
        bool succeeded,
        string? errorCode,
        string? payloadJson,
        DateTime timestamp)
        : base(id)
    {
        CompanyId = companyId;
        UserId = userId;
        ActionType = actionType;
        Succeeded = succeeded;
        ErrorCode = errorCode;
        PayloadJson = payloadJson;
        Timestamp = timestamp;
    }

    public static AuditLog Create(
        CompanyId companyId,
        UserId? userId,
        string actionType,
        bool succeeded,
        string? errorCode,
        string? payloadJson,
        DateTime timestamp)
    {
        return new AuditLog(
            AuditLogId.New(),
            companyId,
            userId,
            actionType,
            succeeded,
            errorCode,
            payloadJson,
            timestamp);
    }
}
