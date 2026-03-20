using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class DriverProfileRepository : IDriverProfileRepository
{
    private readonly ApplicationDbContext _context;

    public DriverProfileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DriverProfile profile, CancellationToken cancellationToken = default)
    {
        await _context.DriverProfiles.AddAsync(profile, cancellationToken);
    }

    public Task<DriverProfile?> GetByEmployeeIdAsync(EmployeeId employeeId, CompanyId companyId, CancellationToken cancellationToken = default) =>
        _context.DriverProfiles.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.CompanyId == companyId, cancellationToken);

    public async Task<IReadOnlyList<DriverProfile>> ListByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default) =>
        await _context.DriverProfiles.Where(x => x.CompanyId == companyId).ToListAsync(cancellationToken);

    public async Task<bool> LicenseExistsAsync(CompanyId companyId, string licenseNumber, EmployeeId? excludingId, CancellationToken cancellationToken = default) =>
        await _context.DriverProfiles.AnyAsync(
            x => x.CompanyId == companyId
                && x.LicenseNumber == licenseNumber
                && (excludingId == null || x.EmployeeId != excludingId),
            cancellationToken);

    public void Remove(DriverProfile profile) =>
        _context.DriverProfiles.Remove(profile);
}
