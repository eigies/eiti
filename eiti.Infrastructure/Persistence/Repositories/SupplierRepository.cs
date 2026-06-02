using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly ApplicationDbContext _context;

    public SupplierRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId, ct);
    }

    public async Task<List<Supplier>> ListAsync(Guid companyId, bool activeOnly, string? search, CancellationToken ct = default)
    {
        var query = _context.Suppliers.Where(s => s.CompanyId == companyId);

        if (activeOnly)
        {
            query = query.Where(s => s.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(normalized) ||
                (s.TaxId != null && s.TaxId.ToLower().Contains(normalized)) ||
                (s.Email != null && s.Email.ToLower().Contains(normalized)));
        }

        return await query.OrderBy(s => s.Name).ToListAsync(ct);
    }

    public async Task<bool> NameExistsAsync(Guid companyId, string name, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        return await _context.Suppliers
            .AnyAsync(s => s.CompanyId == companyId && s.Name.ToLower() == normalized, ct);
    }

    public async Task AddAsync(Supplier supplier, CancellationToken ct = default)
    {
        await _context.Suppliers.AddAsync(supplier, ct);
    }

    public void Update(Supplier supplier)
    {
        _context.Suppliers.Update(supplier);
    }
}
