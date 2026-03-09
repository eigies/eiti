using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Sales;
using eiti.Domain.Transport;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class SaleTransportAssignmentRepository : ISaleTransportAssignmentRepository
{
    private readonly ApplicationDbContext _context;

    public SaleTransportAssignmentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SaleTransportAssignment assignment, CancellationToken cancellationToken = default)
    {
        await _context.SaleTransportAssignments.AddAsync(assignment, cancellationToken);
    }

    public Task<SaleTransportAssignment?> GetByIdAsync(SaleTransportAssignmentId id, CompanyId companyId, CancellationToken cancellationToken = default) =>
        _context.SaleTransportAssignments.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken);

    public Task<SaleTransportAssignment?> GetBySaleIdAsync(SaleId saleId, CompanyId companyId, CancellationToken cancellationToken = default) =>
        _context.SaleTransportAssignments.FirstOrDefaultAsync(x => x.SaleId == saleId && x.CompanyId == companyId, cancellationToken);

    public async Task<IReadOnlyList<SaleTransportAssignment>> ListBySaleIdsAsync(IReadOnlyList<SaleId> saleIds, CompanyId companyId, CancellationToken cancellationToken = default) =>
        await _context.SaleTransportAssignments.Where(x => x.CompanyId == companyId && saleIds.Contains(x.SaleId)).ToListAsync(cancellationToken);
}
