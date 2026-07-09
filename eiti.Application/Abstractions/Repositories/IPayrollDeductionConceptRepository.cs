using eiti.Domain.Companies;
using eiti.Domain.Payroll;

namespace eiti.Application.Abstractions.Repositories;

public interface IPayrollDeductionConceptRepository
{
    Task AddAsync(PayrollDeductionConcept concept, CancellationToken cancellationToken = default);
    Task<PayrollDeductionConcept?> GetByIdAsync(PayrollDeductionConceptId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollDeductionConcept>> ListByCompanyAsync(CompanyId companyId, bool activeOnly, CancellationToken cancellationToken = default);
}
