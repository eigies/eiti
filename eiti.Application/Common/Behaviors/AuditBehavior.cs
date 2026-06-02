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

    public AuditBehavior(ICurrentUserService currentUserService, IAuditLogWriter auditLogWriter)
    {
        _currentUserService = currentUserService;
        _auditLogWriter = auditLogWriter;
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

        try
        {
            var response = await next();
            var (succeeded, errorCode) = InspectResult(response);
            await TryWriteAsync(request, succeeded, errorCode, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            await TryWriteAsync(request, succeeded: false, errorCode: $"Exception.{ex.GetType().Name}", cancellationToken);
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

    private async Task TryWriteAsync(TRequest request, bool succeeded, string? errorCode, CancellationToken cancellationToken)
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
                DateTime.UtcNow);

            await _auditLogWriter.WriteAsync(entry, cancellationToken);
        }
        catch
        {
            // La auditoría nunca debe romper el request.
        }
    }
}
