using eiti.Domain.Companies;
using eiti.Domain.Users;

namespace eiti.Application.Abstractions.Repositories;

public interface IUserRoleAuditRepository
{
    Task AddAsync(UserRoleAudit audit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserRoleAudit>> ListByCompanyAsync(
        CompanyId companyId,
        UserId? targetUserId,
        int take,
        CancellationToken cancellationToken = default);
}
