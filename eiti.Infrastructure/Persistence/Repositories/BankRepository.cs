using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Banks;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class BankRepository : IBankRepository
{
    private readonly ApplicationDbContext _db;

    public BankRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Bank>> ListAsync(bool activeOnly, CancellationToken ct)
    {
        var query = _db.Banks.Include(b => b.InstallmentPlans).AsQueryable();

        if (activeOnly)
        {
            query = query.Where(b => b.Active);
        }

        return await query.OrderBy(b => b.Name).ToListAsync(ct);
    }

    public async Task<Bank?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _db.Banks
            .Include(b => b.InstallmentPlans)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task AddAsync(Bank bank, CancellationToken ct)
    {
        await _db.Banks.AddAsync(bank, ct);
    }
}
