using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.TradeInsByBranch;

public sealed class TradeInsByBranchHandler
    : IRequestHandler<TradeInsByBranchQuery, Result<TradeInsByBranchResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchRepository _branchRepository;

    public TradeInsByBranchHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        IBranchRepository branchRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _branchRepository = branchRepository;
    }

    public async Task<Result<TradeInsByBranchResponse>> Handle(
        TradeInsByBranchQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<TradeInsByBranchResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var from = request.DateFrom.Date;
        var to = request.DateTo.Date.AddDays(1).AddTicks(-1);

        var allowedBranchIds = _currentUserService.CanViewAllBranches
            ? null
            : _currentUserService.AllowedBranchIds;

        // Ventas con canje del período (excluye canceladas, respeta sucursales permitidas).
        var sales = await _saleRepository.ListForTradeInReportAsync(
            companyId, from, to, request.BranchId, request.CustomerId, allowedBranchIds, cancellationToken);

        // Agregación por sucursal + producto.
        var groups = new Dictionary<(Guid BranchId, Guid ProductId), Accumulator>();
        var allSaleIds = new HashSet<Guid>();

        foreach (var sale in sales)
        {
            allSaleIds.Add(sale.Id.Value);

            foreach (var tradeIn in sale.TradeIns)
            {
                var key = (sale.BranchId.Value, tradeIn.ProductId.Value);
                if (!groups.TryGetValue(key, out var acc))
                {
                    acc = new Accumulator();
                    groups[key] = acc;
                }

                acc.SaleIds.Add(sale.Id.Value);
                acc.Units += tradeIn.Quantity;
                acc.Amount += tradeIn.Amount;
            }
        }

        var branchNames = (await _branchRepository.ListByCompanyAsync(companyId, cancellationToken))
            .ToDictionary(b => b.Id.Value, b => b.Name);

        var products = (await _productRepository.GetByCompanyIdAsync(companyId, cancellationToken))
            .ToDictionary(p => p.Id.Value, p => p);

        var rows = groups
            .Select(kvp =>
            {
                var (branchId, productId) = kvp.Key;
                var acc = kvp.Value;
                var amount = decimal.Round(acc.Amount, 2, MidpointRounding.AwayFromZero);
                products.TryGetValue(productId, out var product);

                return new TradeInsByBranchRow(
                    branchId,
                    branchNames.TryGetValue(branchId, out var branchName) ? branchName : "(Sucursal)",
                    productId,
                    product?.Name ?? "(Producto eliminado)",
                    product?.Brand ?? string.Empty,
                    product?.Sku ?? string.Empty,
                    acc.SaleIds.Count,
                    acc.Units,
                    amount,
                    AvgUnitValue(amount, acc.Units));
            })
            .OrderBy(r => r.BranchName)
            .ThenByDescending(r => r.Amount)
            .ToList();

        var totalUnits = rows.Sum(r => r.Units);
        var totalAmount = decimal.Round(rows.Sum(r => r.Amount), 2, MidpointRounding.AwayFromZero);
        var totals = new TradeInsByBranchTotals(
            allSaleIds.Count,
            totalUnits,
            totalAmount,
            AvgUnitValue(totalAmount, totalUnits));

        return Result<TradeInsByBranchResponse>.Success(new TradeInsByBranchResponse(rows, totals));
    }

    private static decimal AvgUnitValue(decimal amount, int units) =>
        units == 0 ? 0m : decimal.Round(amount / units, 2, MidpointRounding.AwayFromZero);

    private sealed class Accumulator
    {
        public HashSet<Guid> SaleIds { get; } = new();
        public int Units { get; set; }
        public decimal Amount { get; set; }
    }
}
