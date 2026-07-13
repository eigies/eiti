using eiti.Domain.Companies;
using eiti.Domain.Payroll;

namespace eiti.Application.Abstractions.Repositories;

public interface IPayrollBonusConceptRepository
{
    Task AddAsync(PayrollBonusConcept concept, CancellationToken cancellationToken = default);
    Task<PayrollBonusConcept?> GetByIdAsync(PayrollBonusConceptId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollBonusConcept>> ListByCompanyAsync(CompanyId companyId, bool activeOnly, CancellationToken cancellationToken = default);
}
