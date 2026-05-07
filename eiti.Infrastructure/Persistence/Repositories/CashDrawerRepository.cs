using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class CashDrawerRepository : ICashDrawerRepository
{
    private readonly ApplicationDbContext _context;

    public CashDrawerRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CashDrawer?> GetByIdAsync(
        CashDrawerId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashDrawers
            .FirstOrDefaultAsync(drawer => drawer.Id == id && drawer.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<CashDrawer>> ListByBranchAsync(
        BranchId branchId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashDrawers
            .Where(drawer => drawer.BranchId == branchId && drawer.CompanyId == companyId)
            .OrderBy(drawer => drawer.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(
        BranchId branchId,
        string name,
        CashDrawerId? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashDrawers.AnyAsync(
            drawer => drawer.BranchId == branchId
                && drawer.Name == name
                && (excludedId == null || drawer.Id != excludedId),
            cancellationToken);
    }

    public async Task AddAsync(CashDrawer cashDrawer, CancellationToken cancellationToken = default)
    {
        await _context.CashDrawers.AddAsync(cashDrawer, cancellationToken);
    }

    public async Task<CashDrawer?> GetByAssignedUserAsync(
        UserId userId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from assignment in _context.CashDrawerUserAssignments
            join drawer in _context.CashDrawers on assignment.CashDrawerId equals drawer.Id
            where assignment.UserId == userId
                && drawer.CompanyId == companyId
                && drawer.IsActive
            select drawer)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, IReadOnlyList<Guid>>> GetAssignedUserIdsByDrawerIdsAsync(
        IEnumerable<Guid> drawerIds,
        CancellationToken cancellationToken = default)
    {
        var ids = drawerIds.Select(id => new CashDrawerId(id)).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        var rows = await _context.CashDrawerUserAssignments
            .Where(assignment => ids.Contains(assignment.CashDrawerId))
            .Select(assignment => new
            {
                CashDrawerId = assignment.CashDrawerId.Value,
                UserId = assignment.UserId.Value
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.CashDrawerId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group.Select(row => row.UserId).OrderBy(id => id).ToList());
    }

    public async Task AssignUsersAsync(
        CashDrawerId drawerId,
        CompanyId companyId,
        IReadOnlyCollection<UserId> userIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserIds = userIds.Distinct().ToList();

        var drawerExists = await _context.CashDrawers.AnyAsync(
            drawer => drawer.Id == drawerId && drawer.CompanyId == companyId,
            cancellationToken);
        if (!drawerExists)
        {
            return;
        }

        var currentDrawerAssignments = await _context.CashDrawerUserAssignments
            .Where(assignment => assignment.CashDrawerId == drawerId)
            .ToListAsync(cancellationToken);

        var currentUserIds = currentDrawerAssignments.Select(assignment => assignment.UserId).ToHashSet();
        var requestedUserIds = normalizedUserIds.ToHashSet();

        var assignmentsToRemoveFromDrawer = currentDrawerAssignments
            .Where(assignment => !requestedUserIds.Contains(assignment.UserId))
            .ToList();

        if (assignmentsToRemoveFromDrawer.Count > 0)
        {
            _context.CashDrawerUserAssignments.RemoveRange(assignmentsToRemoveFromDrawer);
        }

        if (normalizedUserIds.Count > 0)
        {
            var assignmentsToReassign = await _context.CashDrawerUserAssignments
                .Where(assignment => normalizedUserIds.Contains(assignment.UserId) && assignment.CashDrawerId != drawerId)
                .ToListAsync(cancellationToken);

            if (assignmentsToReassign.Count > 0)
            {
                _context.CashDrawerUserAssignments.RemoveRange(assignmentsToReassign);
            }
        }

        var assignmentsToAdd = normalizedUserIds
            .Where(userId => !currentUserIds.Contains(userId))
            .Select(userId => CashDrawerUserAssignment.Create(drawerId, userId))
            .ToList();

        if (assignmentsToAdd.Count > 0)
        {
            await _context.CashDrawerUserAssignments.AddRangeAsync(assignmentsToAdd, cancellationToken);
        }
    }
}
