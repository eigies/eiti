using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;

namespace eiti.Application.Abstractions.Repositories;

public interface IPayrollLiquidationRepository
{
    Task AddAsync(PayrollLiquidation liquidation, CancellationToken cancellationToken = default);
    Task<PayrollLiquidation?> GetByIdAsync(PayrollLiquidationId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<bool> ExistsForPeriodAsync(CompanyId companyId, EmployeeId employeeId, string periodLabel, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollLiquidation>> ListAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        string? periodLabel,
        PayrollLiquidationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> CountAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        string? periodLabel,
        PayrollLiquidationStatus? status,
        CancellationToken cancellationToken = default);
}
