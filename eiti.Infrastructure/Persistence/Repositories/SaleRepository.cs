using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class SaleRepository : ISaleRepository
{
    private readonly ApplicationDbContext _context;

    public SaleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(
        Sale sale,
        CancellationToken cancellationToken = default)
    {
        await _context.Sales.AddAsync(sale, cancellationToken);
    }

    public async Task<Sale?> GetByIdAsync(
        SaleId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(sale => sale.Details)
            .Include(sale => sale.Payments)
            .Include(sale => sale.TradeIns)
            .FirstOrDefaultAsync(sale => sale.Id == id, cancellationToken);
    }

    public void Remove(Sale sale)
    {
        _context.Sales.Remove(sale);
    }

    public async Task<IReadOnlyList<Sale>> ListByCompanyAsync(
        CompanyId companyId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int? idSaleStatus,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Sales
            .Include(sale => sale.Details)
            .Include(sale => sale.Payments)
            .Include(sale => sale.TradeIns)
            .Where(sale => sale.CompanyId == companyId);

        if (dateFrom.HasValue)
        {
            query = query.Where(sale => sale.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            var dateToEndOfDay = dateTo.Value.Date.AddDays(1);
            query = query.Where(sale => sale.CreatedAt < dateToEndOfDay);
        }

        if (idSaleStatus.HasValue)
        {
            query = query.Where(sale => (int)sale.SaleStatus == idSaleStatus.Value);
        }

        return await query
            .OrderByDescending(sale => sale.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalePayment>> GetPaymentsBySaleIdsAsync(
        IEnumerable<Guid> saleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = saleIds.Select(id => new SaleId(id)).ToList();

        return await _context.SalePayments
            .Where(payment => ids.Contains(payment.SaleId))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByBranchAsync(
        BranchId branchId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .CountAsync(sale => sale.BranchId == branchId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SearchDeliveryAddressesAsync(
        string query,
        CompanyId companyId,
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(sale => sale.CompanyId == companyId
                && sale.DeliveryAddress != null
                && sale.DeliveryAddress.Contains(query))
            .Select(sale => sale.DeliveryAddress!)
            .Distinct()
            .OrderBy(address => address)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
