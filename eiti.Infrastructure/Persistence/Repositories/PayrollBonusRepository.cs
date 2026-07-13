using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using eiti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class PayrollBonusRepository : IPayrollBonusRepository
{
    private readonly ApplicationDbContext _context;

    public PayrollBonusRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PayrollBonus bonus, CancellationToken cancellationToken = default)
    {
        await _context.PayrollBonuses.AddAsync(bonus, cancellationToken);
    }

    public async Task<PayrollBonus?> GetByIdAsync(PayrollBonusId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollBonuses
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollBonus>> ListByCompanyAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        PayrollBonusStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollBonuses.Where(x => x.CompanyId == companyId);

        if (employeeId is not null)
            query = query.Where(x => x.EmployeeId == employeeId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollBonus>> ListPendingByEmployeeAsync(CompanyId companyId, EmployeeId employeeId, CancellationToken cancellationToken = default)
    {
        // Tracked (sin AsNoTracking): el batch de liquidacion marca estos bonos como
        // Applied en el mismo SaveChanges que crea la liquidacion.
        return await _context.PayrollBonuses
            .Where(x => x.CompanyId == companyId && x.EmployeeId == employeeId && x.Status == PayrollBonusStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
