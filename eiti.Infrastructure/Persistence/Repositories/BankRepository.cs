using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Banks;
using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class BankRepository : IBankRepository
{
    private readonly ApplicationDbContext _db;

    public BankRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Bank>> ListAsync(
        bool activeOnly,
        CompanyId companyId,
        CancellationToken ct,
        BankUsage usage = BankUsage.All)
    {
        var query = _db.Banks.Include(b => b.InstallmentPlans)
            .Where(b => b.CompanyId == companyId);

        if (activeOnly)
        {
            query = query.Where(b => b.Active);
        }

        query = usage switch
        {
            BankUsage.Card => query.Where(b => b.UseForCard),
            BankUsage.Transfer => query.Where(b => b.UseForTransfer),
            BankUsage.Cheque => query.Where(b => b.UseForCheque),
            _ => query
        };

        return await query.OrderBy(b => b.Name).ToListAsync(ct);
    }

    public async Task<Bank?> GetByIdAsync(int id, CompanyId companyId, CancellationToken ct)
    {
        return await _db.Banks
            .Include(b => b.InstallmentPlans)
            .FirstOrDefaultAsync(b => b.Id == id && b.CompanyId == companyId, ct);
    }

    public async Task<IReadOnlyList<Bank>> GetByIdsAsync(IEnumerable<int> ids, CompanyId companyId, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        return await _db.Banks
            .Include(b => b.InstallmentPlans)
            .Where(b => b.CompanyId == companyId && idList.Contains(b.Id))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Bank bank, CancellationToken ct)
    {
        await _db.Banks.AddAsync(bank, ct);
    }
}
