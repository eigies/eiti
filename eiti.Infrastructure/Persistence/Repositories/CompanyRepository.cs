using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly ApplicationDbContext _context;

    public CompanyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Company?> GetByIdAsync(
        CompanyId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Companies
            .FirstOrDefaultAsync(company => company.Id == id, cancellationToken);
    }

    public async Task<Company?> GetByPrimaryDomainAsync(
        CompanyDomain primaryDomain,
        CancellationToken cancellationToken = default)
    {
        return await _context.Companies
            .FirstOrDefaultAsync(
                company => company.PrimaryDomain == primaryDomain,
                cancellationToken);
    }

    public async Task AddAsync(
        Company company,
        CancellationToken cancellationToken = default)
    {
        await _context.Companies.AddAsync(company, cancellationToken);
    }
}
