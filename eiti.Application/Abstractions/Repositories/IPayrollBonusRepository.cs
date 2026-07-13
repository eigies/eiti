using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;

namespace eiti.Application.Abstractions.Repositories;

public interface IPayrollBonusRepository
{
    Task AddAsync(PayrollBonus bonus, CancellationToken cancellationToken = default);
    Task<PayrollBonus?> GetByIdAsync(PayrollBonusId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollBonus>> ListByCompanyAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        PayrollBonusStatus? status,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollBonus>> ListPendingByEmployeeAsync(CompanyId companyId, EmployeeId employeeId, CancellationToken cancellationToken = default);
}
