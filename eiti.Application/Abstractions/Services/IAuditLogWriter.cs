namespace eiti.Application.Abstractions.Services;

/// <summary>
/// Persiste una entrada de auditoría de forma aislada de la transacción del negocio,
/// para que el registro de auditoría nunca rompa ni dependa del request en curso.
/// </summary>
public interface IAuditLogWriter
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}

public sealed record AuditLogEntry(
    Guid CompanyId,
    Guid? UserId,
    string ActionType,
    bool Succeeded,
    string? ErrorCode,
    string? PayloadJson,
    DateTime TimestampUtc);
