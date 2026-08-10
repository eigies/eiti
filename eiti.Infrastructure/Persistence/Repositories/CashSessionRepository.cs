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

        // Overlap: session intersects the range if OpenedAt <= to AND (ClosedAt is null OR ClosedAt >= from)
        if (from.HasValue)
        {
            query = query.Where(session => session.ClosedAt == null || session.ClosedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(session => session.OpenedAt <= to.Value);
        }

        return await query
            .OrderByDescending(session => session.OpenedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CashSession>> GetAllStaleOpenAsync(
        CompanyId companyId,
        DateTime openedBefore,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashSessions
            .Where(session =>
                session.CompanyId == companyId &&
                session.Status == CashSessionStatus.Open &&
                session.OpenedAt <= openedBefore)
            .OrderBy(session => session.OpenedAt)
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

    public async Task<CashSession?> GetAnyOpenByBranchAsync(
        BranchId branchId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashSessions
            .Include(session => session.Movements)
            .FirstOrDefaultAsync(
                session => session.BranchId == branchId
                    && session.CompanyId == companyId
                    && session.Status == CashSessionStatus.Open,
                cancellationToken);
    }

    public async Task<CashSession?> GetAnyOpenByCompanyAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashSessions
            .Include(session => session.Movements)
            .FirstOrDefaultAsync(
                session => session.CompanyId == companyId
                    && session.Status == CashSessionStatus.Open,
                cancellationToken);
    }

    public async Task<CashSession?> GetByCcPaymentGroupIdAsync(
        Guid ccPaymentGroupId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashSessions
            .Include(session => session.Movements)
            .Where(session => session.CompanyId == companyId
                && session.Movements.Any(movement =>
                    movement.CcPaymentGroupId == ccPaymentGroupId
                    && movement.Type != CashMovementType.CuentaCorrienteCancellation))
            .OrderBy(session => session.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CashSession?> GetBySupplierPaymentIdAsync(
        Guid supplierPaymentId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashSessions
            .Include(session => session.Movements)
            .Where(session => session.CompanyId == companyId
                && session.Movements.Any(movement =>
                    movement.SupplierPaymentId == supplierPaymentId
                    && movement.Type == CashMovementType.PurchaseExpense))
            .OrderBy(session => session.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CashSession?> GetLastClosedByDrawerAsync(
        CashDrawerId cashDrawerId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashSessions
            .Where(session => session.CashDrawerId == cashDrawerId
                && session.CompanyId == companyId
                && session.Status == CashSessionStatus.Closed)
            .OrderByDescending(session => session.ClosedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<HashSet<Guid>> GetOpenDrawerIdsAsync(
        IEnumerable<Guid> drawerIds,
        CancellationToken cancellationToken = default)
    {
        var ids = drawerIds.Select(id => new CashDrawerId(id)).ToList();
        if (ids.Count == 0)
            return [];

        var result = await _context.CashSessions
            .Where(s => ids.Contains(s.CashDrawerId) && s.Status == CashSessionStatus.Open)
            .Select(s => s.CashDrawerId.Value)
            .ToListAsync(cancellationToken);

        return result.ToHashSet();
    }

    public async Task AddAsync(CashSession cashSession, CancellationToken cancellationToken = default)
    {
        await _context.CashSessions.AddAsync(cashSession, cancellationToken);
    }

    public async Task<IReadOnlyList<CashMovement>> ListMovementsByCompanyAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        IReadOnlyCollection<CashMovementType> types,
        IReadOnlyCollection<Guid>? branchIds = null,
        CancellationToken cancellationToken = default)
    {
        var typeList = types.ToList();

        var sessions = _context.CashSessions
            .AsNoTracking()
            .Where(session => session.CompanyId == companyId);

        if (branchIds is not null)
        {
            // Comparar el value object entero: acceder a .Value dentro del arbol de expresion
            // no lo traduce EF y tira InvalidOperationException. Mismo patron que SaleRepository.
            var allowed = branchIds.Select(id => new BranchId(id)).ToList();
            sessions = sessions.Where(session => allowed.Contains(session.BranchId));
        }

        return await sessions
            .SelectMany(session => session.Movements)
            .Where(movement => movement.OccurredAt >= from
                && movement.OccurredAt <= to
                && typeList.Contains(movement.Type))
            .OrderBy(movement => movement.OccurredAt)
            .ToListAsync(cancellationToken);
    }
}
