using eiti.Domain.Suppliers;

namespace eiti.Application.Abstractions.Repositories;

public interface ISupplierPaymentRepository
{
    Task<SupplierPayment?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    Task<List<SupplierPayment>> ListBySupplierAsync(Guid companyId, Guid supplierId, CancellationToken ct = default);

    Task AddAsync(SupplierPayment payment, CancellationToken ct = default);
}
