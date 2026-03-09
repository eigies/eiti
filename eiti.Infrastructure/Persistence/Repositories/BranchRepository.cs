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
}
