using eiti.Domain.Companies;
using eiti.Domain.Users;

namespace eiti.Application.Abstractions.Repositories;

public interface IAccessProfileRepository
{
    Task<AccessProfile?> GetByIdAsync(AccessProfileId id, CancellationToken cancellationToken = default);
    Task<AccessProfile?> GetBySystemKeyAsync(CompanyId companyId, string systemKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccessProfile>> ListByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(CompanyId companyId, string name, AccessProfileId? excludingId = null, CancellationToken cancellationToken = default);
    Task<bool> HasUsersAssignedAsync(AccessProfileId id, CancellationToken cancellationToken = default);
    Task AddAsync(AccessProfile profile, CancellationToken cancellationToken = default);
    void Remove(AccessProfile profile);
}
