using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;

namespace eiti.Application.Abstractions.Repositories;

public interface IPayrollAdvanceRepository
{
    Task AddAsync(PayrollAdvance advance, CancellationToken cancellationToken = default);
    Task<PayrollAdvance?> GetByIdAsync(PayrollAdvanceId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollAdvance>> ListByCompanyAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        PayrollAdvanceStatus? status,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollAdvance>> ListPendingByEmployeeAsync(CompanyId companyId, EmployeeId employeeId, CancellationToken cancellationToken = default);
}
