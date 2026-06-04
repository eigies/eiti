using eiti.Application.Abstractions.Services;
using eiti.Domain.Audit;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using eiti.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace eiti.Infrastructure.Services;

/// <summary>
/// Escribe la auditoría en un scope/DbContext propio, aislado de la transacción
/// del request, de modo que persista incluso si el handler falla y no contamine
/// el SaveChanges del negocio.
/// </summary>
public sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AuditLogWriter(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var auditLog = AuditLog.Create(
            new CompanyId(entry.CompanyId),
            entry.UserId.HasValue ? new UserId(entry.UserId.Value) : null,
            entry.ActionType,
            entry.Succeeded,
            entry.ErrorCode,
            entry.PayloadJson,
            entry.BeforeJson,
            entry.AfterJson,
            entry.TimestampUtc);

        await context.AuditLogs.AddAsync(auditLog, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
