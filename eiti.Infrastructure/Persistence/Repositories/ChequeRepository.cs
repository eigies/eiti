using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class ChequeRepository : IChequeRepository
{
    private readonly ApplicationDbContext _db;

    public ChequeRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Cheque>> ListAsync(ChequeFilters filters, CompanyId companyId, CancellationToken ct)
    {
        var query = _db.Cheques.Where(c => c.CompanyId == companyId);

        if (filters.Estado.HasValue)
        {
            query = query.Where(c => c.Estado == filters.Estado.Value);
        }

        if (filters.BankId.HasValue)
        {
            query = query.Where(c => c.BankId == filters.BankId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Numero))
        {
            var numero = filters.Numero.Trim();
            query = query.Where(c => c.Numero.Contains(numero));
        }

        if (filters.FechaVencFrom.HasValue)
        {
            query = query.Where(c => c.FechaVencimiento >= filters.FechaVencFrom.Value.Date);
        }

        if (filters.FechaVencTo.HasValue)
        {
            query = query.Where(c => c.FechaVencimiento <= filters.FechaVencTo.Value.Date);
        }

        return await query.OrderBy(c => c.FechaVencimiento).ToListAsync(ct);
    }

    public async Task<Cheque?> GetByIdAsync(Guid id, CompanyId companyId, CancellationToken ct)
    {
        return await _db.Cheques.FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId, ct);
    }

    public async Task<IReadOnlyList<Cheque>> ListByIdsAsync(IEnumerable<Guid> ids, CompanyId companyId, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        return await _db.Cheques
            .Where(c => c.CompanyId == companyId && idList.Contains(c.Id))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Cheque cheque, CancellationToken ct)
    {
        await _db.Cheques.AddAsync(cheque, ct);
    }
}
