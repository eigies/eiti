using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class SupplierPaymentRepository : ISupplierPaymentRepository
{
    private readonly ApplicationDbContext _db;

    public SupplierPaymentRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SupplierPayment?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await _db.SupplierPayments
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId, ct);
    }

    public async Task<List<SupplierPayment>> ListBySupplierAsync(Guid companyId, Guid supplierId, CancellationToken ct = default)
    {
        return await _db.SupplierPayments
            .Where(p => p.CompanyId == companyId && p.SupplierId == supplierId)
            .OrderByDescending(p => p.Date)
            .ToListAsync(ct);
    }

    public async Task AddAsync(SupplierPayment payment, CancellationToken ct = default)
    {
        await _db.SupplierPayments.AddAsync(payment, ct);
    }
}
