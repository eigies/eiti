using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using eiti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class PayrollAdvanceRepository : IPayrollAdvanceRepository
{
    private readonly ApplicationDbContext _context;

    public PayrollAdvanceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PayrollAdvance advance, CancellationToken cancellationToken = default)
    {
        await _context.PayrollAdvances.AddAsync(advance, cancellationToken);
    }

    public async Task<PayrollAdvance?> GetByIdAsync(PayrollAdvanceId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollAdvances
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollAdvance>> ListByCompanyAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        PayrollAdvanceStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollAdvances.Where(x => x.CompanyId == companyId);

        if (employeeId is not null)
            query = query.Where(x => x.EmployeeId == employeeId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await query.OrderByDescending(x => x.Date).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollAdvance>> ListPendingByEmployeeAsync(CompanyId companyId, EmployeeId employeeId, CancellationToken cancellationToken = default)
    {
        // Tracked (sin AsNoTracking): el batch de liquidacion marca estos adelantos como
        // Applied en el mismo SaveChanges que crea la liquidacion.
        return await _context.PayrollAdvances
            .Where(x => x.CompanyId == companyId && x.EmployeeId == employeeId && x.Status == PayrollAdvanceStatus.Pending)
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);
    }
}
