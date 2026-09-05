using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class SupplierCreditNoteRepository : ISupplierCreditNoteRepository
{
    private readonly ApplicationDbContext _db;

    public SupplierCreditNoteRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SupplierCreditNote?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await _db.SupplierCreditNotes
            .FirstOrDefaultAsync(n => n.Id == id && n.CompanyId == companyId, ct);
    }

    public async Task<List<SupplierCreditNote>> ListBySupplierAsync(Guid companyId, Guid supplierId, CancellationToken ct = default)
    {
        return await _db.SupplierCreditNotes
            .Where(n => n.CompanyId == companyId && n.SupplierId == supplierId)
            .OrderByDescending(n => n.Date)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> CountByBranchAsync(Guid companyId, Guid branchId, CancellationToken ct = default)
    {
        return await _db.SupplierCreditNotes
            .CountAsync(n => n.CompanyId == companyId && n.BranchId == branchId, ct);
    }

    public async Task AddAsync(SupplierCreditNote note, CancellationToken ct = default)
    {
        await _db.SupplierCreditNotes.AddAsync(note, ct);
    }
}
