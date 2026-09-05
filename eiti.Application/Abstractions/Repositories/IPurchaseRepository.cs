using eiti.Domain.Purchases;

namespace eiti.Application.Abstractions.Repositories;

public interface IPurchaseRepository
{
    Task<Purchase?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    Task<List<Purchase>> ListAsync(
        Guid companyId,
        Guid? supplierId,
        PurchaseStatus? status,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<int> CountAsync(
        Guid companyId,
        Guid? supplierId,
        PurchaseStatus? status,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);

    Task AddAsync(Purchase purchase, CancellationToken ct = default);

    Task<Dictionary<Guid, string?>> GetCodesByPurchaseIdsAsync(
        IEnumerable<Guid> purchaseIds,
        CancellationToken ct = default);

    Task<bool> ExistsWithInvoiceNumberAsync(
        Guid companyId,
        Guid? supplierId,
        string invoiceNumber,
        CancellationToken ct = default);

    Task<Dictionary<Guid, decimal>> GetPendingTotalsBySupplierAsync(
        Guid companyId,
        CancellationToken ct = default);

    Task<List<Purchase>> ListPendingBySupplierAsync(
        Guid companyId,
        Guid supplierId,
        CancellationToken ct = default);

    Task<List<Purchase>> ListAllBySupplierAsync(
        Guid companyId,
        Guid supplierId,
        CancellationToken ct = default);

    Task<List<Purchase>> ListBySupplierPaymentIdAsync(
        Guid companyId,
        Guid supplierPaymentId,
        CancellationToken ct = default);

    // Compras con imputaciones activas de una nota de crédito, para deshacerlas al anularla. Tracked.
    Task<List<Purchase>> ListByCreditNoteIdAsync(
        Guid companyId,
        Guid creditNoteId,
        CancellationToken ct = default);
}
