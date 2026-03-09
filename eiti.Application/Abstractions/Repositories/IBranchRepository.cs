using eiti.Domain.Branches;
using eiti.Domain.Companies;

namespace eiti.Application.Abstractions.Repositories;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(
        BranchId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Branch>> ListByCompanyAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        CompanyId companyId,
        string name,
        BranchId? excludedId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Branch branch,
        CancellationToken cancellationToken = default);
}
