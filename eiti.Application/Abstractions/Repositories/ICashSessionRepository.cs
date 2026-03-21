using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;

namespace eiti.Application.Abstractions.Repositories;

public interface ICashSessionRepository
{
    Task<CashSession?> GetByIdAsync(
        CashSessionId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<CashSession?> GetOpenByDrawerAsync(
        CashDrawerId cashDrawerId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashSession>> ListByDrawerAsync(
        CashDrawerId cashDrawerId,
        CompanyId companyId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    Task<CashSession?> GetOpenForBranchAsync(
        BranchId branchId,
        CashDrawerId cashDrawerId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<CashSession?> GetAnyOpenByBranchAsync(
        BranchId branchId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<CashSession?> GetLastClosedByDrawerAsync(
        CashDrawerId cashDrawerId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        CashSession cashSession,
        CancellationToken cancellationToken = default);
}
