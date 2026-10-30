using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Addresses;
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
    private readonly ISaleFiscalDocumentRepository _fiscalDocuments;
    private readonly IAddressRepository _addressRepository;

    public GetSaleByIdHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        ISaleFiscalDocumentRepository fiscalDocuments,
        IAddressRepository addressRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _fiscalDocuments = fiscalDocuments;
        _addressRepository = addressRepository;
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

        var branchAccess = _currentUserService.EnsureBranchAccess(sale.BranchId.Value);
        if (branchAccess.IsFailure)
            return Result<GetSaleByIdResponse>.Failure(branchAccess.Error);

        Customer? customer = null;
        if (sale.CustomerId is not null)
        {
            customer = await _customerRepository.GetByIdAsync(sale.CustomerId, companyId, cancellationToken);
        }

        string? customerAddress = null;
        if (customer?.AddressId is not null)
        {
            var address = await _addressRepository.GetByIdAsync(customer.AddressId, cancellationToken);
            customerAddress = address is null ? null : FormatAddress(address);
        }

        var productIds = sale.Details.Select(d => d.ProductId)
            .Concat(sale.TradeIns.Select(t => t.ProductId))
            .Distinct().ToList();
        var productMap = productIds.Count == 0
            ? new Dictionary<Guid, Product>()
            : (await _productRepository.GetByIdsAsync(productIds, companyId, cancellationToken))
                .ToDictionary(p => p.Id.Value);

        var fiscalDocuments = await _fiscalDocuments.ListBySaleIdsAsync([sale.Id], companyId, cancellationToken);
        var invoicing = SaleInvoicingView.From(fiscalDocuments);
        var invoice = invoicing.Invoice;
        var creditNote = invoicing.CreditNote;
        var debitNote = invoicing.DebitNote;

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
                customerAddress,
                string.IsNullOrWhiteSpace(customer?.Phone) ? null : customer.Phone,
                sale.DeliveryAddress,
                sale.ContactPhone,
                sale.CashDrawerId?.Value,
                sale.HasDelivery,
                (int)sale.SaleStatus,
                sale.SaleStatus.ToString(),
                sale.IsCuentaCorriente,
                sale.GeneralDiscountPercent,
                sale.OriginalTotal,
                sale.TotalAmount,
                sale.VatRate,
                sale.VatAmount,
                sale.CardSurchargeTotal,
                sale.ManualOverridePrice,
                sale.OverriddenByUserId,
                sale.OverriddenAt,
                sale.MonetaryPaidAmount,
                sale.TradeInAmount,
                sale.SettledAmount,
                sale.PendingAmount,
                sale.CcPaidTotal,
                sale.CcPendingAmount,
                sale.CreatedAt,
                sale.PaidAt,
                sale.UpdatedAt,
                (int)(invoice?.Status ?? SaleInvoicingStatus.NotInvoiced),
                (invoice?.Status ?? SaleInvoicingStatus.NotInvoiced).ToString(),
                invoice?.FiscalDocumentId,
                invoice?.DocumentType,
                invoice?.PointOfSale,
                invoice?.Number,
                invoice?.AuthorizationCode,
                invoice?.AuthorizationExpiry,
                invoice?.QrUrl,
                invoice?.RejectionReason ?? creditNote?.RejectionReason,
                invoice?.IssuedAt,
                (int?)creditNote?.Status,
                creditNote?.Number,
                (int?)debitNote?.Status,
                debitNote?.Number,
                sale.Details.Select(detail => new GetSaleByIdDetailResponse(
                    detail.ProductId.Value,
                    GetProductName(productMap, detail.ProductId.Value),
                    GetProductBrand(productMap, detail.ProductId.Value),
                    detail.Quantity,
                    detail.UnitPrice,
                    detail.DiscountPercent,
                    detail.TotalAmount)).ToList(),
                sale.Payments.Select(payment => new GetSaleByIdPaymentResponse(
                    (int)payment.Method,
                    payment.Method.ToString(),
                    payment.Amount,
                    payment.Reference,
                    payment.TransferBankId)).ToList(),
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
                    payment.CancelledAt,
                    payment.GroupId)).ToList(),
                sale.TradeIns.Select(tradeIn => new GetSaleByIdTradeInResponse(
                    tradeIn.ProductId.Value,
                    GetProductName(productMap, tradeIn.ProductId.Value),
                    GetProductBrand(productMap, tradeIn.ProductId.Value),
                    tradeIn.Quantity,
                    tradeIn.Amount)).ToList(),
                sale.Notes));
    }

    private static string GetProductName(IDictionary<Guid, Product> productMap, Guid productId)
    {
        return productMap.TryGetValue(productId, out var product) ? product.Name : "Deleted product";
    }

    private static string GetProductBrand(IDictionary<Guid, Product> productMap, Guid productId)
    {
        return productMap.TryGetValue(productId, out var product) ? product.Brand : "Unknown";
    }

    private static string? FormatAddress(Address address)
    {
        if (string.IsNullOrWhiteSpace(address.Street))
            return null;

        var street = address.Street;
        if (!string.IsNullOrWhiteSpace(address.StreetNumber))
            street += $" {address.StreetNumber}";
        if (!string.IsNullOrWhiteSpace(address.Floor))
            street += $", Piso {address.Floor}";
        if (!string.IsNullOrWhiteSpace(address.Apartment))
            street += $", Depto {address.Apartment}";

        return street;
    }
}
