using eiti.Domain.Audit;
using eiti.Domain.Companies;
using eiti.Domain.Users;

namespace eiti.Application.Abstractions.Repositories;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLog>> ListAsync(
        CompanyId companyId,
        UserId? userId,
        DateTime from,
        DateTime to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        CompanyId companyId,
        UserId? userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}
