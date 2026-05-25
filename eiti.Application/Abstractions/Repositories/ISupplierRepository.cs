using eiti.Domain.Suppliers;

namespace eiti.Application.Abstractions.Repositories;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    Task<List<Supplier>> ListAsync(Guid companyId, bool activeOnly, string? search, CancellationToken ct = default);

    Task<bool> NameExistsAsync(Guid companyId, string name, CancellationToken ct = default);

    Task AddAsync(Supplier supplier, CancellationToken ct = default);
}
