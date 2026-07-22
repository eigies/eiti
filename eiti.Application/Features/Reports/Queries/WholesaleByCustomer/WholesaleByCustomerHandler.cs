using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.WholesaleByCustomer;

public sealed class WholesaleByCustomerHandler
    : IRequestHandler<WholesaleByCustomerQuery, Result<WholesaleByCustomerResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;

    public WholesaleByCustomerHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        ICustomerRepository customerRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<WholesaleByCustomerResponse>> Handle(
        WholesaleByCustomerQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<WholesaleByCustomerResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var saleType = (request.SaleType ?? "wholesale").ToLowerInvariant();

        var from = request.DateFrom.Date;
        var to = request.DateTo.Date.AddDays(1).AddTicks(-1);

        var allowedBranchIds = _currentUserService.CanViewAllBranches
            ? null
            : _currentUserService.AllowedBranchIds;

        // Ventas del período (excluye canceladas, respeta sucursales permitidas). Solo Include(Details).
        var sales = (await _saleRepository.ListForSalesReportAsync(
                companyId, from, to, request.BranchId, request.CustomerId, allowedBranchIds, cancellationToken))
            .ToList();

        // Mayorista = Cuenta Corriente; Minorista = venta normal. "all" no filtra.
        sales = saleType switch
        {
            "wholesale" => sales.Where(s => s.IsCuentaCorriente).ToList(),
            "retail" => sales.Where(s => !s.IsCuentaCorriente).ToList(),
            _ => sales
        };

        var products = (await _productRepository.GetByCompanyIdAsync(companyId, cancellationToken))
            .ToDictionary(p => p.Id.Value, p => p.CostPrice);

        // Saldo de CC pendiente ACTUAL por cliente (todas sus ventas CC, incluye pagos). Mismo criterio
        // que el reporte de deudores. Respeta sucursales permitidas y excluye canceladas.
        var ccSales = (await _saleRepository.ListCcSalesByCompanyAsync(companyId, null, cancellationToken))
            .Where(s => s.SaleStatus != SaleStatus.Cancel && s.CustomerId is not null && s.CcPendingAmount > 0)
            .ToList();
        if (allowedBranchIds is not null)
            ccSales = ccSales.Where(s => allowedBranchIds.Contains(s.BranchId.Value)).ToList();
        var ccPendingByCustomer = ccSales
            .GroupBy(s => s.CustomerId!.Value)
            .ToDictionary(
                g => g.Key,
                g => decimal.Round(g.Sum(s => s.CcPendingAmount), 2, MidpointRounding.AwayFromZero));

        // Agregación por cliente (null = consumidor final).
        var groups = new Dictionary<Guid?, Accumulator>();
        foreach (var sale in sales)
        {
            var customerKey = sale.CustomerId?.Value;
            if (!groups.TryGetValue(customerKey, out var acc))
            {
                acc = new Accumulator();
                groups[customerKey] = acc;
            }

            acc.SaleIds.Add(sale.Id.Value);
            foreach (var line in sale.Details)
            {
                products.TryGetValue(line.ProductId.Value, out var catalogCost);
                var unitCost = line.UnitCost ?? catalogCost;
                acc.Units += line.Quantity;
                acc.Revenue += line.TotalAmount;
                acc.Cost += decimal.Round(line.Quantity * unitCost, 2, MidpointRounding.AwayFromZero);
            }
        }

        // Nombres de clientes en un solo query.
        var customerIds = groups.Keys.Where(k => k is not null).Select(k => k!.Value).ToList();
        var customerNames = customerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _customerRepository.ListByIdsAsync(companyId, customerIds.Select(id => new CustomerId(id)), cancellationToken))
                .ToDictionary(c => c.Id.Value, c => c.FullName);

        var rows = groups
            .Select(kvp =>
            {
                var acc = kvp.Value;
                var operations = acc.SaleIds.Count;
                var revenue = decimal.Round(acc.Revenue, 2, MidpointRounding.AwayFromZero);
                var cost = decimal.Round(acc.Cost, 2, MidpointRounding.AwayFromZero);
                var profit = revenue - cost;
                var name = kvp.Key is null
                    ? "(Consumidor final)"
                    : customerNames.TryGetValue(kvp.Key.Value, out var n) ? n : "(Cliente)";
                var ccPending = kvp.Key is not null && ccPendingByCustomer.TryGetValue(kvp.Key.Value, out var pending)
                    ? pending
                    : 0m;

                return new WholesaleByCustomerRow(
                    kvp.Key,
                    name,
                    operations,
                    acc.Units,
                    revenue,
                    AvgTicket(revenue, operations),
                    ccPending,
                    cost,
                    profit,
                    Margin(revenue, profit));
            })
            .OrderByDescending(r => r.Revenue)
            .ToList();

        var totalOperations = rows.Sum(r => r.Operations);
        var totalRevenue = rows.Sum(r => r.Revenue);
        var totalCost = rows.Sum(r => r.Cost);
        var totals = new WholesaleByCustomerTotals(
            totalOperations,
            rows.Sum(r => r.Units),
            totalRevenue,
            AvgTicket(totalRevenue, totalOperations),
            rows.Sum(r => r.CcPending),
            totalCost,
            totalRevenue - totalCost,
            Margin(totalRevenue, totalRevenue - totalCost));

        return Result<WholesaleByCustomerResponse>.Success(
            new WholesaleByCustomerResponse(saleType, rows, totals));
    }

    private static decimal AvgTicket(decimal revenue, int operations) =>
        operations == 0 ? 0m : decimal.Round(revenue / operations, 2, MidpointRounding.AwayFromZero);

    private static decimal Margin(decimal revenue, decimal profit) =>
        revenue == 0 ? 0m : decimal.Round(profit / revenue * 100, 2, MidpointRounding.AwayFromZero);

    private sealed class Accumulator
    {
        public HashSet<Guid> SaleIds { get; } = new();
        public int Units { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
    }
}
