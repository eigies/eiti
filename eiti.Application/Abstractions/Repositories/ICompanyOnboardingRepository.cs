using eiti.Domain.Companies;

namespace eiti.Application.Abstractions.Repositories;

public interface ICompanyOnboardingRepository
{
    Task<CompanyOnboarding?> GetByCompanyIdAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        CompanyOnboarding onboarding,
        CancellationToken cancellationToken = default);
}
