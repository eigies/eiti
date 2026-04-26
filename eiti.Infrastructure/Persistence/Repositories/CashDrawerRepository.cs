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
        return await _context.CashDrawers
            .FirstOrDefaultAsync(
                drawer => drawer.AssignedUserId == userId
                    && drawer.CompanyId == companyId
                    && drawer.IsActive,
                cancellationToken);
    }
}
