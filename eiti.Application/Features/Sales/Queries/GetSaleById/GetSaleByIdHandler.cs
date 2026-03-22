using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.GetSaleById;

public sealed class GetSaleByIdHandler : IRequestHandler<GetSaleByIdQuery, Result<GetSaleByIdResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;

    public GetSaleByIdHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<GetSaleByIdResponse>> Handle(GetSaleByIdQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GetSaleByIdResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;
        if (companyId is null)
        {
            return Result<GetSaleByIdResponse>.Failure(
                Error.Unauthorized("Sales.GetById.Unauthorized", "The current user is not authenticated."));
        }

        var sale = await _saleRepository.GetByIdWithCcPaymentsAsync(new SaleId(request.SaleId), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result<GetSaleByIdResponse>.Failure(
                Error.NotFound("Sales.GetById.NotFound", "The sale was not found."));
        }

        Customer? customer = null;
        if (sale.CustomerId is not null)
        {
            customer = await _customerRepository.GetByIdAsync(sale.CustomerId, companyId, cancellationToken);
        }

        var productIds = sale.Details.Select(d => d.ProductId).Distinct().ToList();
        var productMap = new Dictionary<Guid, Product>();
        foreach (var productId in productIds)
        {
            var product = await _productRepository.GetByIdAsync(productId, companyId, cancellationToken);
            if (product is not null)
            {
                productMap[product.Id.Value] = product;
            }
        }

        string? customerDocument = null;
        if (customer?.DocumentType is not null && !string.IsNullOrWhiteSpace(customer.DocumentNumber))
        {
            customerDocument = $"{customer.DocumentType} {customer.DocumentNumber}";
        }

        return Result<GetSaleByIdResponse>.Success(
            new GetSaleByIdResponse(
                sale.Id.Value,
                sale.Code,
                sale.BranchId.Value,
                sale.CustomerId?.Value,
                customer?.FullName,
                customerDocument,
                customer?.TaxId,
                sale.HasDelivery,
                (int)sale.SaleStatus,
                sale.SaleStatus.ToString(),
                sale.IsCuentaCorriente,
                sale.TotalAmount,
                sale.MonetaryPaidAmount,
                sale.TradeInAmount,
                sale.SettledAmount,
                sale.PendingAmount,
                sale.CcPaidTotal,
                sale.CcPendingAmount,
                sale.CreatedAt,
                sale.PaidAt,
                sale.UpdatedAt,
                sale.Details.Select(detail => new GetSaleByIdDetailResponse(
                    detail.ProductId.Value,
                    GetProductName(productMap, detail.ProductId.Value),
                    GetProductBrand(productMap, detail.ProductId.Value),
                    detail.Quantity,
                    detail.UnitPrice,
                    detail.TotalAmount)).ToList(),
                sale.Payments.Select(payment => new GetSaleByIdPaymentResponse(
                    (int)payment.Method,
                    payment.Method.ToString(),
                    payment.Amount,
                    payment.Reference)).ToList(),
                sale.CcPayments.Select(payment => new GetSaleByIdCcPaymentResponse(
                    payment.Id.Value,
                    (int)payment.Method,
                    payment.Method.ToString(),
                    payment.Amount,
                    payment.Date,
                    payment.Notes,
                    (int)payment.Status,
                    payment.Status.ToString(),
                    payment.CreatedAt,
                    payment.CancelledAt)).ToList()));
    }

    private static string GetProductName(IDictionary<Guid, Product> productMap, Guid productId)
    {
        return productMap.TryGetValue(productId, out var product) ? product.Name : "Deleted product";
    }

    private static string GetProductBrand(IDictionary<Guid, Product> productMap, Guid productId)
    {
        return productMap.TryGetValue(productId, out var product) ? product.Brand : "Unknown";
    }
}
