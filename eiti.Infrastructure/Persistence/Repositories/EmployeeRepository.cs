using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly ApplicationDbContext _context;

    public EmployeeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        await _context.Employees.AddAsync(employee, cancellationToken);
    }

    public Task<Employee?> GetByIdAsync(EmployeeId id, CompanyId companyId, CancellationToken cancellationToken = default) =>
        _context.Employees.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);

    public async Task<IReadOnlyList<Employee>> ListByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default) =>
        await _context.Employees.Where(x => x.CompanyId == companyId).OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Employee>> ListDriversByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default) =>
        await _context.Employees.Where(x => x.CompanyId == companyId && x.EmployeeRole == EmployeeRole.Driver).OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(cancellationToken);

    public async Task<bool> DocumentExistsAsync(CompanyId companyId, string documentNumber, EmployeeId? excludingId, CancellationToken cancellationToken = default) =>
        await _context.Employees.AnyAsync(
            x => x.CompanyId == companyId
                && x.DocumentNumber == documentNumber
                && (excludingId == null || x.Id != excludingId),
            cancellationToken);
}
