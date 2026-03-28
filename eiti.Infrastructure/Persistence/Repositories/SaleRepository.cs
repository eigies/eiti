using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
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
            .Where(sale => sale.CompanyId == companyId)
            .Where(sale => !sale.IsCuentaCorriente);

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

    public async Task<Sale?> GetByIdWithCcPaymentsAsync(
        SaleId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(sale => sale.Details)
            .Include(sale => sale.Payments)
            .Include(sale => sale.TradeIns)
            .Include(sale => sale.CcPayments)
            .FirstOrDefaultAsync(sale => sale.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Sale>> ListCcSalesByCompanyAsync(
        CompanyId companyId,
        CustomerId? customerId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Sales
            .Include(sale => sale.Details)
            .Include(sale => sale.CcPayments)
            .Where(sale => sale.CompanyId == companyId && sale.IsCuentaCorriente);

        if (customerId is not null)
        {
            query = query.Where(sale => sale.CustomerId == customerId);
        }

        return await query
            .OrderByDescending(sale => sale.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Sale>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var saleIds = ids.Select(id => new SaleId(id)).ToList();

        return await _context.Sales
            .Where(sale => saleIds.Contains(sale.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, Guid>> GetSaleIdsByCcPaymentIdsAsync(
        IEnumerable<Guid> ccPaymentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = ccPaymentIds.Select(id => new SaleCcPaymentId(id)).ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, Guid>();

        return await _context.SaleCcPayments
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                Id = p.Id.Value,
                SaleId = p.SaleId.Value
            })
            .ToDictionaryAsync(x => x.Id, x => x.SaleId, cancellationToken);
    }
}
