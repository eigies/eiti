using eiti.Domain.Companies;
using eiti.Domain.Employees;

namespace eiti.Application.Abstractions.Repositories;

public interface IDriverProfileRepository
{
    Task AddAsync(DriverProfile profile, CancellationToken cancellationToken = default);
    Task<DriverProfile?> GetByEmployeeIdAsync(EmployeeId employeeId, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriverProfile>> ListByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default);
    Task<bool> LicenseExistsAsync(CompanyId companyId, string licenseNumber, EmployeeId? excludingId, CancellationToken cancellationToken = default);
    void Remove(DriverProfile profile);
}
