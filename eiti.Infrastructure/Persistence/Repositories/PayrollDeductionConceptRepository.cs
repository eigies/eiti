using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using eiti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class PayrollDeductionConceptRepository : IPayrollDeductionConceptRepository
{
    private readonly ApplicationDbContext _context;

    public PayrollDeductionConceptRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PayrollDeductionConcept concept, CancellationToken cancellationToken = default)
    {
        await _context.PayrollDeductionConcepts.AddAsync(concept, cancellationToken);
    }

    public async Task<PayrollDeductionConcept?> GetByIdAsync(PayrollDeductionConceptId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollDeductionConcepts
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollDeductionConcept>> ListByCompanyAsync(CompanyId companyId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollDeductionConcepts.Where(x => x.CompanyId == companyId);

        if (activeOnly)
            query = query.Where(x => x.IsActive);

        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }
}
