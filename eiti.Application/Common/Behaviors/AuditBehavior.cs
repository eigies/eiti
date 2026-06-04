using eiti.Application.Abstractions.Services;
using MediatR;

namespace eiti.Application.Common.Behaviors;

/// <summary>
/// Registra en la auditoría cada comando ejecutado (acciones que modifican datos).
/// Se registra como el behavior más externo del pipeline para capturar el resultado
/// final, incluyendo fallos de autorización (Forbidden) y de validación.
/// Las queries (lecturas) se ignoran por convención de nombre.
/// </summary>
public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IAuditSnapshotService _auditSnapshotService;

    public AuditBehavior(
        ICurrentUserService currentUserService,
        IAuditLogWriter auditLogWriter,
        IAuditSnapshotService auditSnapshotService)
    {
        _currentUserService = currentUserService;
        _auditLogWriter = auditLogWriter;
        _auditSnapshotService = auditSnapshotService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!typeof(TRequest).Name.EndsWith("Command", StringComparison.Ordinal))
        {
            return await next();
        }

        var companyId = _currentUserService.CompanyId;
        var beforeJson = companyId is not null
            ? await TryCaptureBeforeAsync(request, companyId.Value, cancellationToken)
            : null;

        try
        {
            var response = await next();
            var (succeeded, errorCode) = InspectResult(response);
            var afterJson = succeeded && companyId is not null
                ? await TryCaptureAfterAsync(request, response, companyId.Value, cancellationToken)
                : null;

            await TryWriteAsync(request, succeeded, errorCode, beforeJson, afterJson, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            await TryWriteAsync(
                request,
                succeeded: false,
                errorCode: $"Exception.{ex.GetType().Name}",
                beforeJson,
                afterJson: null,
                cancellationToken);
            throw;
        }
    }

    private static (bool Succeeded, string? ErrorCode) InspectResult(TResponse response)
    {
        if (response is Common.Result result)
        {
            return result.IsSuccess
                ? (true, null)
                : (false, result.Error.Code);
        }

        return (true, null);
    }

    private async Task<string?> TryCaptureBeforeAsync(TRequest request, Guid companyId, CancellationToken cancellationToken)
    {
        try
        {
            return await _auditSnapshotService.CaptureBeforeAsync(request, companyId, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryCaptureAfterAsync(
        TRequest request,
        TResponse response,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _auditSnapshotService.CaptureAfterAsync(request, response, succeeded: true, companyId, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task TryWriteAsync(
        TRequest request,
        bool succeeded,
        string? errorCode,
        string? beforeJson,
        string? afterJson,
        CancellationToken cancellationToken)
    {
        try
        {
            var companyId = _currentUserService.CompanyId;
            if (companyId is null)
            {
                // Sin compañía no se puede asociar la auditoría al tenant; se omite.
                return;
            }

            var entry = new AuditLogEntry(
                companyId.Value,
                _currentUserService.UserId?.Value,
                typeof(TRequest).Name,
                succeeded,
                errorCode,
                AuditPayloadSerializer.Serialize(request),
                beforeJson,
                afterJson,
                DateTime.UtcNow);

            await _auditLogWriter.WriteAsync(entry, cancellationToken);
        }
        catch
        {
            // La auditoría nunca debe romper el request.
        }
    }
}
