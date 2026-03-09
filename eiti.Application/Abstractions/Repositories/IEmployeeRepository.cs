using eiti.Domain.Companies;
using eiti.Domain.Employees;

namespace eiti.Application.Abstractions.Repositories;

public interface IEmployeeRepository
{
    Task AddAsync(Employee employee, CancellationToken cancellationToken = default);
    Task<Employee?> GetByIdAsync(EmployeeId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Employee>> ListByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Employee>> ListDriversByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default);
    Task<bool> DocumentExistsAsync(CompanyId companyId, string documentNumber, EmployeeId? excludingId, CancellationToken cancellationToken = default);
}
