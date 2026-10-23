using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class SupplierPaymentOrderRepository : ISupplierPaymentOrderRepository
{
    private readonly ApplicationDbContext _db;

    public SupplierPaymentOrderRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SupplierPaymentOrder?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await _db.SupplierPaymentOrders
            .FirstOrDefaultAsync(o => o.Id == id && o.CompanyId == companyId, ct);
    }

    public async Task<int> CountByCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        return await _db.SupplierPaymentOrders
            .CountAsync(o => o.CompanyId == companyId, ct);
    }

    public async Task AddAsync(SupplierPaymentOrder order, CancellationToken ct = default)
    {
        await _db.SupplierPaymentOrders.AddAsync(order, ct);
    }
}
