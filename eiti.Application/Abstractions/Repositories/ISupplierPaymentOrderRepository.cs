using eiti.Domain.Suppliers;

namespace eiti.Application.Abstractions.Repositories;

public interface ISupplierPaymentOrderRepository
{
    Task<SupplierPaymentOrder?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    // Para numerar OP-#### por empresa. Cuenta todas, incluidas las anuladas: el número emitido
    // no se reutiliza.
    Task<int> CountByCompanyAsync(Guid companyId, CancellationToken ct = default);

    Task AddAsync(SupplierPaymentOrder order, CancellationToken ct = default);
}
