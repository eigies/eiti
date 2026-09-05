using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Purchases;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class PurchaseRepository : IPurchaseRepository
{
    private readonly ApplicationDbContext _context;

    public PurchaseRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Purchase?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await _context.Purchases
            .Include(p => p.Details)
            .Include(p => p.Payments)
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId, ct);
    }

    public async Task<List<Purchase>> ListAsync(
        Guid companyId,
        Guid? supplierId,
        PurchaseStatus? status,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = BuildQuery(companyId, supplierId, status, from, to);

        return await query
            .Include(p => p.Details)
            .Include(p => p.Payments)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(
        Guid companyId,
        Guid? supplierId,
        PurchaseStatus? status,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default)
    {
        return await BuildQuery(companyId, supplierId, status, from, to).CountAsync(ct);
    }

    public async Task AddAsync(Purchase purchase, CancellationToken ct = default)
    {
        await _context.Purchases.AddAsync(purchase, ct);
    }

    public async Task<Dictionary<Guid, string?>> GetCodesByPurchaseIdsAsync(
        IEnumerable<Guid> purchaseIds,
        CancellationToken ct = default)
    {
        var ids = purchaseIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string?>();

        return await _context.Purchases
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Code })
            .ToDictionaryAsync(x => x.Id, x => (string?)x.Code, ct);
    }

    public async Task<bool> ExistsWithInvoiceNumberAsync(
        Guid companyId,
        Guid? supplierId,
        string invoiceNumber,
        CancellationToken ct = default)
    {
        return await _context.Purchases
            .AnyAsync(p =>
                p.CompanyId == companyId &&
                p.SupplierId == supplierId &&
                p.InvoiceNumber == invoiceNumber &&
                p.Status != PurchaseStatus.Cancelled, ct);
    }

    public async Task<Dictionary<Guid, decimal>> GetPendingTotalsBySupplierAsync(
        Guid companyId,
        CancellationToken ct = default)
    {
        var purchases = await _context.Purchases
            .Include(p => p.Details)
            .Include(p => p.Payments)
            .Where(p => p.CompanyId == companyId
                && p.Status == PurchaseStatus.Active)
            .ToListAsync(ct);

        return purchases
            .GroupBy(p => p.SupplierId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.PendingAmount));
    }

    public async Task<List<Purchase>> ListAllBySupplierAsync(
        Guid companyId,
        Guid supplierId,
        CancellationToken ct = default)
    {
        return await _context.Purchases
            .Include(p => p.Details)
            .Include(p => p.Payments)
            .Where(p => p.CompanyId == companyId && p.SupplierId == supplierId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<Purchase>> ListBySupplierPaymentIdAsync(
        Guid companyId,
        Guid supplierPaymentId,
        CancellationToken ct = default)
    {
        // Compras que tienen filas de imputación generadas por ese pago de proveedor (tracked, para revertir).
        return await _context.Purchases
            .Include(p => p.Details)
            .Include(p => p.Payments)
            .Where(p => p.CompanyId == companyId
                && p.Payments.Any(pay => pay.SupplierPaymentId == supplierPaymentId))
            .ToListAsync(ct);
    }

    public async Task<List<Purchase>> ListByCreditNoteIdAsync(
        Guid companyId,
        Guid creditNoteId,
        CancellationToken ct = default)
    {
        return await _context.Purchases
            .Include(p => p.Details)
            .Include(p => p.Payments)
            .Where(p => p.CompanyId == companyId
                && p.Payments.Any(pay => pay.CreditNoteId == creditNoteId))
            .ToListAsync(ct);
    }

    public async Task<List<Purchase>> ListPendingBySupplierAsync(
        Guid companyId,
        Guid supplierId,
        CancellationToken ct = default)
    {
        // Compras con saldo pendiente del proveedor (Active = no pagada ni cancelada), más vieja primero (FIFO).
        // Tracked (sin AsNoTracking): el saldo a favor aplicado en cascada se persiste en el SaveChanges del handler.
        return await _context.Purchases
            .Include(p => p.Details)
            .Include(p => p.Payments)
            .Where(p => p.CompanyId == companyId
                && p.SupplierId == supplierId
                && p.Status == PurchaseStatus.Active)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    private IQueryable<Purchase> BuildQuery(
        Guid companyId,
        Guid? supplierId,
        PurchaseStatus? status,
        DateTime? from,
        DateTime? to)
    {
        var query = _context.Purchases.Where(p => p.CompanyId == companyId);

        if (supplierId.HasValue)
            query = query.Where(p => p.SupplierId == supplierId.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (from.HasValue)
            query = query.Where(p => p.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(p => p.CreatedAt <= to.Value);

        return query;
    }
}
