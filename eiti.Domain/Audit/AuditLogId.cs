namespace eiti.Domain.Audit;

public sealed record AuditLogId(Guid Value)
{
    public static AuditLogId New() => new(Guid.NewGuid());
}
