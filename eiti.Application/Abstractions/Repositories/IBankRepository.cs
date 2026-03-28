using eiti.Domain.Banks;

namespace eiti.Application.Abstractions.Repositories;

public interface IBankRepository
{
    Task<IReadOnlyList<Bank>> ListAsync(bool activeOnly, CancellationToken ct);
    Task<Bank?> GetByIdAsync(int id, CancellationToken ct);
    Task AddAsync(Bank bank, CancellationToken ct);
}
