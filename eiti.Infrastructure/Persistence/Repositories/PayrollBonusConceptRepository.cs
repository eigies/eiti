using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using eiti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class PayrollBonusConceptRepository : IPayrollBonusConceptRepository
{
    private readonly ApplicationDbContext _context;

    public PayrollBonusConceptRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PayrollBonusConcept concept, CancellationToken cancellationToken = default)
    {
        await _context.PayrollBonusConcepts.AddAsync(concept, cancellationToken);
    }

    public async Task<PayrollBonusConcept?> GetByIdAsync(PayrollBonusConceptId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollBonusConcepts
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollBonusConcept>> ListByCompanyAsync(CompanyId companyId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollBonusConcepts.Where(x => x.CompanyId == companyId);

        if (activeOnly)
            query = query.Where(x => x.IsActive);

        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }
}
