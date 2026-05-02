using eiti.Domain.Banks;
using eiti.Domain.Companies;

namespace eiti.Application.Abstractions.Repositories;

public interface IBankRepository
{
    Task<IReadOnlyList<Bank>> ListAsync(bool activeOnly, CompanyId companyId, CancellationToken ct);
    Task<Bank?> GetByIdAsync(int id, CompanyId companyId, CancellationToken ct);
    Task AddAsync(Bank bank, CancellationToken ct);
}
