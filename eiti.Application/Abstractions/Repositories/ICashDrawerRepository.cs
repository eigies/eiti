using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Users;

namespace eiti.Application.Abstractions.Repositories;

public interface ICashDrawerRepository
{
    Task<CashDrawer?> GetByIdAsync(
        CashDrawerId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashDrawer>> ListByBranchAsync(
        BranchId branchId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        BranchId branchId,
        string name,
        CashDrawerId? excludedId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        CashDrawer cashDrawer,
        CancellationToken cancellationToken = default);

    Task<CashDrawer?> GetByAssignedUserAsync(
        UserId userId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);
}
