using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using eiti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class PayrollLiquidationRepository : IPayrollLiquidationRepository
{
    private readonly ApplicationDbContext _context;

    public PayrollLiquidationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PayrollLiquidation liquidation, CancellationToken cancellationToken = default)
    {
        await _context.PayrollLiquidations.AddAsync(liquidation, cancellationToken);
    }

    public async Task<PayrollLiquidation?> GetByIdAsync(PayrollLiquidationId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollLiquidations
            .Include(x => x.DeductionLines)
            .Include(x => x.AdvanceLines)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);
    }

    public async Task<bool> ExistsForPeriodAsync(CompanyId companyId, EmployeeId employeeId, string periodLabel, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollLiquidations.AnyAsync(
            x => x.CompanyId == companyId
                && x.EmployeeId == employeeId
                && x.PeriodLabel == periodLabel
                && x.Status != PayrollLiquidationStatus.Cancelled,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollLiquidation>> ListAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        string? periodLabel,
        PayrollLiquidationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(companyId, employeeId, periodLabel, status)
            .Include(x => x.DeductionLines)
            .Include(x => x.AdvanceLines)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        CompanyId companyId,
        EmployeeId? employeeId,
        string? periodLabel,
        PayrollLiquidationStatus? status,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(companyId, employeeId, periodLabel, status).CountAsync(cancellationToken);
    }

    private IQueryable<PayrollLiquidation> BuildQuery(
        CompanyId companyId,
        EmployeeId? employeeId,
        string? periodLabel,
        PayrollLiquidationStatus? status)
    {
        var query = _context.PayrollLiquidations.Where(x => x.CompanyId == companyId);

        if (employeeId is not null)
            query = query.Where(x => x.EmployeeId == employeeId);

        if (!string.IsNullOrWhiteSpace(periodLabel))
            query = query.Where(x => x.PeriodLabel == periodLabel);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return query;
    }
}
