using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
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
            .Include(sale => sale.CcPayments)
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
        bool includeCuentaCorriente = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Sales
            .Include(sale => sale.Details)
            .Include(sale => sale.Payments)
            .Include(sale => sale.TradeIns)
            .Where(sale => sale.CompanyId == companyId)
            .Where(sale => includeCuentaCorriente || !sale.IsCuentaCorriente);

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

    public async Task<IReadOnlyList<Sale>> ListForSalesReportAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? branchId,
        Guid? customerId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Sales
            .AsNoTracking()
            .Include(sale => sale.Details)
            .Where(sale => sale.CompanyId == companyId
                && sale.CreatedAt >= from
                && sale.CreatedAt <= to
                && sale.SaleStatus != SaleStatus.Cancel);

        if (branchId.HasValue)
        {
            var bId = new BranchId(branchId.Value);
            query = query.Where(sale => sale.BranchId == bId);
        }

        if (customerId.HasValue)
        {
            var cId = new CustomerId(customerId.Value);
            query = query.Where(sale => sale.CustomerId == cId);
        }

        if (allowedBranchIds is not null && allowedBranchIds.Count > 0)
        {
            var allowed = allowedBranchIds.Select(id => new BranchId(id)).ToList();
            query = query.Where(sale => allowed.Contains(sale.BranchId));
        }

        return await query
            .OrderByDescending(sale => sale.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Sale>> ListWithPaymentsForReportAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? branchId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Sales
            .AsNoTracking()
            .Include(sale => sale.Payments)
            .Where(sale => sale.CompanyId == companyId
                && sale.CreatedAt >= from
                && sale.CreatedAt <= to
                && sale.SaleStatus != SaleStatus.Cancel);

        if (branchId.HasValue)
        {
            var bId = new BranchId(branchId.Value);
            query = query.Where(sale => sale.BranchId == bId);
        }

        if (allowedBranchIds is not null && allowedBranchIds.Count > 0)
        {
            var allowed = allowedBranchIds.Select(id => new BranchId(id)).ToList();
            query = query.Where(sale => allowed.Contains(sale.BranchId));
        }

        return await query
            .OrderByDescending(sale => sale.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasOnHoldSalesByCashDrawerAsync(
        CompanyId companyId,
        CashDrawerId cashDrawerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Sales.AnyAsync(
            sale => sale.CompanyId == companyId
                && !sale.IsCuentaCorriente
                && sale.CashDrawerId == cashDrawerId
                && sale.SaleStatus == SaleStatus.OnHold,
            cancellationToken);
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

    public async Task<IReadOnlyList<SalePayment>> GetPaymentsByCashSessionIdAsync(
        CashSessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        var saleIds = await _context.Sales
            .Where(sale => sale.CashSessionId == sessionId && sale.SaleStatus != SaleStatus.Cancel)
            .Select(sale => sale.Id)
            .ToListAsync(cancellationToken);

        if (saleIds.Count == 0)
            return [];

        return await _context.SalePayments
            .Where(payment => saleIds.Contains(payment.SaleId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalePayment>> GetPaymentsByCashSessionIdsAsync(
        IEnumerable<CashSessionId> sessionIds,
        CancellationToken cancellationToken = default)
    {
        var ids = sessionIds.ToList();
        if (ids.Count == 0)
            return [];

        var saleIds = await _context.Sales
            .Where(sale => sale.CashSessionId != null
                && ids.Contains(sale.CashSessionId)
                && sale.SaleStatus != SaleStatus.Cancel)
            .Select(sale => sale.Id)
            .ToListAsync(cancellationToken);

        if (saleIds.Count == 0)
            return [];

        return await _context.SalePayments
            .Where(payment => saleIds.Contains(payment.SaleId))
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

    public async Task<IReadOnlyList<Sale>> ListPendingCcSalesByCustomerAsync(
        CompanyId companyId,
        CustomerId customerId,
        CancellationToken cancellationToken = default)
    {
        // CC activas (no canceladas) del cliente, más vieja primero (FIFO). Tracked: las imputaciones se persisten
        // en el SaveChanges del handler. El filtro de pendiente > 0 se aplica en memoria (CcPendingAmount es computado).
        var sales = await _context.Sales
            .Include(sale => sale.Details)
            .Include(sale => sale.CcPayments)
            .Where(sale => sale.CompanyId == companyId
                && sale.CustomerId == customerId
                && sale.IsCuentaCorriente
                && sale.SaleStatus != SaleStatus.Cancel)
            .OrderBy(sale => sale.CreatedAt)
            .ToListAsync(cancellationToken);

        // El filtro de estado se re-chequea en memoria: el SQL evalúa SaleStatus contra el valor persistido,
        // pero un handler puede haber cancelado una venta (aún sin SaveChanges) cuyos cobros CC quedaron inactivos,
        // reinflando su CcPendingAmount. Sin este re-chequeo, esa venta recién cancelada se reprocesaría como
        // pendiente y ApplyCustomerCredit lanzaría "Cannot add payments to a cancelled sale."
        return sales
            .Where(sale => sale.SaleStatus != SaleStatus.Cancel && sale.CcPendingAmount > 0)
            .ToList();
    }

    public async Task<IReadOnlyList<Sale>> ListCcSalesByCustomerAsync(
        CompanyId companyId,
        CustomerId customerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(sale => sale.Details)
            .Include(sale => sale.CcPayments)
            .Where(sale => sale.CompanyId == companyId
                && sale.CustomerId == customerId
                && sale.IsCuentaCorriente)
            .OrderByDescending(sale => sale.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Sale>> ListByCustomerPaymentIdAsync(
        CompanyId companyId,
        Guid customerPaymentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(sale => sale.Details)
            .Include(sale => sale.CcPayments)
            .Where(sale => sale.CompanyId == companyId
                && sale.CcPayments.Any(p => p.CustomerPaymentId == customerPaymentId))
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, decimal>> GetPendingCcTotalsByCustomerAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        var sales = await _context.Sales
            .Include(sale => sale.CcPayments)
            .Where(sale => sale.CompanyId == companyId
                && sale.IsCuentaCorriente
                && sale.SaleStatus != SaleStatus.Cancel
                && sale.CustomerId != null)
            .ToListAsync(cancellationToken);

        return sales
            .Where(sale => sale.CustomerId != null && sale.CcPendingAmount > 0)
            .GroupBy(sale => sale.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(sale => sale.CcPendingAmount));
    }

    public async Task<IReadOnlyList<Sale>> ListReservingByProductAsync(
        CompanyId companyId,
        ProductId productId,
        BranchId? branchId,
        CancellationToken cancellationToken = default)
    {
        // Las ventas en OnHold (pendientes) son las que sostienen la reserva de stock.
        var query = _context.Sales
            .Include(sale => sale.Details)
            .Include(sale => sale.CcPayments)
            .Where(sale => sale.CompanyId == companyId
                        && sale.SaleStatus == SaleStatus.OnHold
                        && sale.Details.Any(detail => detail.ProductId == productId));

        if (branchId is not null)
        {
            query = query.Where(sale => sale.BranchId == branchId);
        }

        return await query
            .OrderBy(sale => sale.CreatedAt)
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

    public async Task<Dictionary<Guid, string?>> GetCodesBySaleIdsAsync(
        IEnumerable<Guid> saleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = saleIds.Select(id => new SaleId(id)).ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string?>();

        return await _context.Sales
            .Where(sale => ids.Contains(sale.Id))
            .Select(sale => new { Id = sale.Id.Value, sale.Code })
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
    }

    public async Task<IReadOnlyList<SaleCcPayment>> GetCcPaymentsByGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken cancellationToken = default)
    {
        var ids = groupIds.ToList();
        if (ids.Count == 0)
            return [];

        return await _context.SaleCcPayments
            .Where(p => p.GroupId.HasValue && ids.Contains(p.GroupId!.Value))
            .ToListAsync(cancellationToken);
    }
}
