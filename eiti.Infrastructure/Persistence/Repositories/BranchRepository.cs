using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class BranchRepository : IBranchRepository
{
    private readonly ApplicationDbContext _context;

    public BranchRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Branch?> GetByIdAsync(
        BranchId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .FirstOrDefaultAsync(branch => branch.Id == id && branch.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<Branch>> ListByCompanyAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .Where(branch => branch.CompanyId == companyId)
            .OrderBy(branch => branch.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(
        CompanyId companyId,
        string name,
        BranchId? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        return await _context.Branches.AnyAsync(
            branch => branch.CompanyId == companyId
                && branch.Name == name
                && (excludedId == null || branch.Id != excludedId),
            cancellationToken);
    }

    public async Task AddAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        await _context.Branches.AddAsync(branch, cancellationToken);
    }

    public async Task<bool> IsReferencedAsync(
        BranchId branchId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        if (await _context.Sales.AnyAsync(sale => sale.BranchId == branchId, cancellationToken))
            return true;

        if (await _context.CashDrawers.AnyAsync(drawer => drawer.BranchId == branchId, cancellationToken))
            return true;

        if (await _context.StockMovements.AnyAsync(movement => movement.BranchId == branchId, cancellationToken))
            return true;

        if (await _context.UserBranchAccesses.AnyAsync(access => access.BranchId == branchId, cancellationToken))
            return true;

        if (await _context.BranchProductStocks.AnyAsync(
                stock => stock.BranchId == branchId
                    && (stock.OnHandQuantity > 0 || stock.ReservedQuantity > 0),
                cancellationToken))
            return true;

        return false;
    }

    public async Task DeleteAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        var emptyStocks = await _context.BranchProductStocks
            .Where(stock => stock.BranchId == branch.Id
                && stock.OnHandQuantity == 0
                && stock.ReservedQuantity == 0)
            .ToListAsync(cancellationToken);

        if (emptyStocks.Count > 0)
            _context.BranchProductStocks.RemoveRange(emptyStocks);

        _context.Branches.Remove(branch);
    }
}
