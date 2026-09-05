using eiti.Domain.Suppliers;

namespace eiti.Application.Abstractions.Repositories;

public interface ISupplierCreditNoteRepository
{
    Task<SupplierCreditNote?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    Task<List<SupplierCreditNote>> ListBySupplierAsync(Guid companyId, Guid supplierId, CancellationToken ct = default);

    // Para numerar NCP-### por sucursal. Cuenta todas, incluidas las anuladas: el número
    // emitido no se reutiliza.
    Task<int> CountByBranchAsync(Guid companyId, Guid branchId, CancellationToken ct = default);

    Task AddAsync(SupplierCreditNote note, CancellationToken ct = default);
}
