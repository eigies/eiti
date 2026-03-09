using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class CashSessionRepository : ICashSessionRepository
{
    private readonly ApplicationDbContext _context;

    public CashSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CashSession?> GetByIdAsync(
        CashSessionId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashSessions
            .Include(session => session.Movements)
            .FirstOrDefaultAsync(
                session => session.Id == id && session.CompanyId == companyId,
                cancellationToken);
    }

    public async Task<CashSession?> GetOpenByDrawerAsync(
        CashDrawerId cashDrawerId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashSessions
            .Include(session => session.Movements)
            .FirstOrDefaultAsync(
                session => session.CashDrawerId == cashDrawerId
                    && session.CompanyId == companyId
                    && session.Status == CashSessionStatus.Open,
                cancellationToken);
    }

    public async Task<IReadOnlyList<CashSession>> ListByDrawerAsync(
        CashDrawerId cashDrawerId,
        CompanyId companyId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CashSessions
            .Include(session => session.Movements)
            .Where(session => session.CashDrawerId == cashDrawerId && session.CompanyId == companyId);

        if (from.HasValue)
        {
            query = query.Where(session => session.OpenedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(session => session.OpenedAt <= to.Value);
        }

        return await query
            .OrderByDescending(session => session.OpenedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CashSession?> GetOpenForBranchAsync(
        BranchId branchId,
        CashDrawerId cashDrawerId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashSessions
            .Include(session => session.Movements)
            .FirstOrDefaultAsync(
                session => session.BranchId == branchId
                    && session.CashDrawerId == cashDrawerId
                    && session.CompanyId == companyId
                    && session.Status == CashSessionStatus.Open,
                cancellationToken);
    }

    public async Task AddAsync(CashSession cashSession, CancellationToken cancellationToken = default)
    {
        await _context.CashSessions.AddAsync(cashSession, cancellationToken);
    }
}
