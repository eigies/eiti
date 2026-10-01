using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class FiscalPointOfSaleRepository : IFiscalPointOfSaleRepository
{
    private readonly ApplicationDbContext _context;

    public FiscalPointOfSaleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> NumberInUseAsync(CompanyId companyId, int number, FiscalPointOfSaleId? excluding, CancellationToken cancellationToken = default)
    {
        return await _context.FiscalPointsOfSale.AnyAsync(
            pointOfSale => pointOfSale.CompanyId == companyId && pointOfSale.Number == number
                && (excluding == null || pointOfSale.Id != excluding),
            cancellationToken);
    }

    public async Task<FiscalPointOfSale?> GetByIdAsync(FiscalPointOfSaleId id, CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.FiscalPointsOfSale
            .FirstOrDefaultAsync(pointOfSale => pointOfSale.Id == id && pointOfSale.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<FiscalPointOfSale>> ListByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.FiscalPointsOfSale
            .Where(pointOfSale => pointOfSale.CompanyId == companyId)
            .OrderBy(pointOfSale => pointOfSale.Number)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FiscalPointOfSale pointOfSale, CancellationToken cancellationToken = default)
    {
        await _context.FiscalPointsOfSale.AddAsync(pointOfSale, cancellationToken);
    }

    public void Remove(FiscalPointOfSale pointOfSale)
    {
        _context.FiscalPointsOfSale.Remove(pointOfSale);
    }
}
