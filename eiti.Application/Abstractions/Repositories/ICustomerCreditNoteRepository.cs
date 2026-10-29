using eiti.Domain.Customers;

namespace eiti.Application.Abstractions.Repositories;

public interface ICustomerCreditNoteRepository
{
    Task<CustomerCreditNote?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    Task<List<CustomerCreditNote>> ListByCustomerAsync(Guid companyId, Guid customerId, CancellationToken ct = default);

    // Las emitidas sobre la venta (SaleId), vigentes y anuladas: anular la venta exige que no quede ninguna vigente.
    Task<List<CustomerCreditNote>> ListBySaleAsync(Guid companyId, Guid saleId, CancellationToken ct = default);

    // Para numerar NCC-### por sucursal. Cuenta todas, incluidas las anuladas, igual que
    // CountByBranchAsync de ventas: el número emitido no se reutiliza.
    Task<int> CountByBranchAsync(Guid companyId, Guid branchId, CancellationToken ct = default);

    Task AddAsync(CustomerCreditNote note, CancellationToken ct = default);
}
