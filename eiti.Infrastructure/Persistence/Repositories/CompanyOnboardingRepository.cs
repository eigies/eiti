using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class CompanyOnboardingRepository : ICompanyOnboardingRepository
{
    private readonly ApplicationDbContext _context;

    public CompanyOnboardingRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<CompanyOnboarding?> GetByCompanyIdAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return _context.Set<CompanyOnboarding>()
            .FirstOrDefaultAsync(onboarding => onboarding.Id == companyId, cancellationToken);
    }

    public async Task AddAsync(CompanyOnboarding onboarding, CancellationToken cancellationToken = default)
    {
        await _context.Set<CompanyOnboarding>().AddAsync(onboarding, cancellationToken);
    }
}
