using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.DailySalesControl;

public sealed class DailySalesControlHandler
    : IRequestHandler<DailySalesControlQuery, Result<DailySalesControlResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IBranchRepository _branchRepository;

    public DailySalesControlHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        ICustomerRepository customerRepository,
        IBranchRepository branchRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
        _branchRepository = branchRepository;
    }

    public async Task<Result<DailySalesControlResponse>> Handle(
        DailySalesControlQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<DailySalesControlResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var requestedStatus = request.Status == 0 ? (int?)null : request.Status;
        var sales = (await _saleRepository.ListByCompanyAsync(
                companyId,
                request.DateFrom.Date,
                request.DateTo.Date,
                requestedStatus,
                includeCuentaCorriente: true,
                cancellationToken))
            .ToList();

        if (request.Status == 0)
            sales = sales.Where(sale => sale.SaleStatus != SaleStatus.Cancel).ToList();

        if (!_currentUserService.CanViewAllBranches)
        {
            var allowedBranchIds = _currentUserService.AllowedBranchIds;
            sales = sales
                .Where(sale => allowedBranchIds.Contains(sale.BranchId.Value))
                .ToList();
        }

        var products = (await _productRepository.GetByCompanyIdAsync(companyId, cancellationToken))
            .ToDictionary(product => product.Id.Value);
        var branches = (await _branchRepository.ListByCompanyAsync(companyId, cancellationToken))
            .ToDictionary(branch => branch.Id.Value, branch => branch.Name);

        var customers = new Dictionary<Guid, string>();
        foreach (var customerId in sales
                     .Where(sale => sale.CustomerId is not null)
                     .Select(sale => sale.CustomerId!.Value)
                     .Distinct())
        {
            var customer = await _customerRepository.GetByIdAsync(
                new CustomerId(customerId),
                companyId,
                cancellationToken);
            if (customer is not null)
                customers[customerId] = customer.FullName;
        }

        var rows = sales
            .OrderByDescending(sale => sale.CreatedAt)
            .Select(sale => new DailySalesControlRow(
                sale.Id.Value,
                sale.Code,
                sale.CreatedAt,
                sale.BranchId.Value,
                branches.GetValueOrDefault(sale.BranchId.Value, "Sucursal"),
                sale.CustomerId?.Value,
                sale.CustomerId is not null
                    ? customers.GetValueOrDefault(sale.CustomerId.Value, "Cliente")
                    : "Consumidor final",
                (int)sale.SaleStatus,
                sale.SaleStatus.ToString(),
                sale.IsCuentaCorriente,
                sale.TotalAmount,
                sale.Details.Select(detail =>
                {
                    products.TryGetValue(detail.ProductId.Value, out var product);
                    return new DailySalesProductItem(
                        detail.ProductId.Value,
                        product?.Code ?? "-",
                        product?.Brand ?? "Sin marca",
                        product?.Name ?? "Producto eliminado",
                        detail.Quantity,
                        detail.UnitPrice,
                        detail.TotalAmount);
                }).ToList(),
                sale.Payments.Select(payment => new DailySalesPaymentItem(
                    (int)payment.Method,
                    payment.Method.ToString(),
                    payment.Amount,
                    payment.Reference)).ToList(),
                sale.TradeIns.Select(tradeIn =>
                {
                    products.TryGetValue(tradeIn.ProductId.Value, out var product);
                    return new DailySalesTradeInItem(
                        tradeIn.ProductId.Value,
                        product?.Code ?? "-",
                        product?.Brand ?? "Sin marca",
                        product?.Name ?? "Producto eliminado",
                        tradeIn.Quantity,
                        tradeIn.Amount);
                }).ToList()))
            .ToList();

        var totals = new DailySalesControlTotals(
            rows.Count,
            rows.Sum(row => row.Products.Sum(product => product.Quantity)),
            rows.Count(row => row.TradeIns.Count > 0),
            rows.Sum(row => row.TotalAmount));

        return Result<DailySalesControlResponse>.Success(
            new DailySalesControlResponse(rows, totals));
    }
}
