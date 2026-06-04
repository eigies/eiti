namespace eiti.Application.Abstractions.Services;

public interface IAuditSnapshotService
{
    Task<string?> CaptureBeforeAsync(
        object request,
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<string?> CaptureAfterAsync(
        object request,
        object? response,
        bool succeeded,
        Guid companyId,
        CancellationToken cancellationToken = default);
}
