using eiti.Domain.Companies;

namespace eiti.Application.Abstractions.Repositories;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(
        CompanyId id,
        CancellationToken cancellationToken = default);

    Task<Company?> GetByPrimaryDomainAsync(
        CompanyDomain primaryDomain,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Company company,
        CancellationToken cancellationToken = default);
}
