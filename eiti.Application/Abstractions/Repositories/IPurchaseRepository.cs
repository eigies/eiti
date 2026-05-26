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
}
