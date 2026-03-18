using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Transport;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.UpdateSale;

public sealed class UpdateSaleHandler : IRequestHandler<UpdateSaleCommand, Result<UpdateSaleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISaleTransportAssignmentRepository _saleTransportAssignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSaleHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ICashSessionRepository cashSessionRepository,
        ISaleTransportAssignmentRepository saleTransportAssignmentRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _cashSessionRepository = cashSessionRepository;
        _saleTransportAssignmentRepository = saleTransportAssignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UpdateSaleResponse>> Handle(UpdateSaleCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<UpdateSaleResponse>.Failure(authCheck.Error);
        var companyId = _currentUserService.CompanyId!;

        if (!Enum.IsDefined(typeof(SaleStatus), request.IdSaleStatus))
        {
            return Result<UpdateSaleResponse>.Failure(UpdateSaleErrors.InvalidStatus);
        }

        var sale = await _saleRepository.GetByIdAsync(new SaleId(request.Id), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result<UpdateSaleResponse>.Failure(UpdateSaleErrors.NotFound);
        }

        if (sale.SaleStatus != SaleStatus.OnHold)
        {
            return Result<UpdateSaleResponse>.Failure(UpdateSaleErrors.NotEditable);
        }

        var existingTransportAssignmentId = sale.TransportAssignmentId;

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _customerRepository.GetByIdAsync(new CustomerId(request.CustomerId.Value), companyId, cancellationToken);
            if (customer is null)
            {
                return Result<UpdateSaleResponse>.Failure(UpdateSaleErrors.CustomerNotFound);
            }
        }

        var groupedDetails = request.Details
            .GroupBy(detail => detail.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                UnitPrice = group.FirstOrDefault(i => i.UnitPrice.HasValue)?.UnitPrice
            })
            .ToList();
        var currentGroupedDetails = sale.Details
            .GroupBy(detail => detail.ProductId.Value)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                UnitPrice = group.First().UnitPrice
            })
            .ToList();

        var requestedStatus = (SaleStatus)request.IdSaleStatus;

        if (requestedStatus == SaleStatus.Paid && !_currentUserService.HasPermission(PermissionCodes.SalesPay))
        {
            return Result<UpdateSaleResponse>.Failure(UpdateSaleErrors.PaymentForbidden);
        }

        var productMap = new Dictionary<Guid, Product>();
        var saleDetails = new List<SaleDetail>();
        var stockMap = new Dictionary<Guid, BranchProductStock>();

        foreach (var detail in groupedDetails)
        {
            var product = await _productRepository.GetByIdAsync(
                new ProductId(detail.ProductId),
                companyId,
                cancellationToken);

            if (product is null)
            {
                return Result<UpdateSaleResponse>.Failure(
                    Error.NotFound("Sales.Update.ProductNotFound", $"The product '{detail.ProductId}' was not found."));
            }

            productMap[product.Id.Value] = product;
            var stock = await _branchProductStockRepository.GetOrCreateAsync(
                sale.BranchId,
                product.Id,
                companyId,
                cancellationToken);

            stockMap[product.Id.Value] = stock;
            decimal unitPrice;
            if (detail.UnitPrice.HasValue &&
                detail.UnitPrice.Value >= 0 &&
                _currentUserService.HasPermission(PermissionCodes.SalesPriceOverride))
            {
                unitPrice = detail.UnitPrice.Value;
            }
            else
            {
                unitPrice = product.Price;
            }
            saleDetails.Add(SaleDetail.Create(product.Id, detail.Quantity, unitPrice));
        }

        foreach (var currentDetail in currentGroupedDetails)
        {
            var stock = await _branchProductStockRepository.GetOrCreateAsync(
                sale.BranchId,
                new ProductId(currentDetail.ProductId),
                companyId,
                cancellationToken);

            try
            {
                stock.ReleaseReservation(currentDetail.Quantity);
            }
            catch (ArgumentException ex)
            {
                return Result<UpdateSaleResponse>.Failure(
                    Error.Validation("Sales.Update.InvalidQuantity", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Result<UpdateSaleResponse>.Failure(
                    Error.Conflict("Sales.Update.InvalidReservation", ex.Message));
            }

            await _stockMovementRepository.AddAsync(
                StockMovement.Create(
                    companyId,
                    sale.BranchId,
                    stock.ProductId,
                    stock.Id,
                    StockMovementType.ReleaseReservation,
                    currentDetail.Quantity,
                    "Sale",
                    sale.Id.Value,
                    "Stock reservation released.",
                    _currentUserService.UserId),
                cancellationToken);
        }

        IReadOnlyList<SalePayment> salePayments = sale.Payments.ToList();
        IReadOnlyList<SaleTradeIn> saleTradeIns = sale.TradeIns.ToList();

        if (requestedStatus != SaleStatus.Cancel)
        {
            try
            {
                salePayments = BuildPayments(request.Payments);
            }
            catch (ArgumentException ex)
            {
                return Result<UpdateSaleResponse>.Failure(
                    Error.Validation("Sales.Update.InvalidPayments", ex.Message));
            }

            var tradeInsResult = await BuildTradeInsAsync(request.TradeIns, productMap, companyId, cancellationToken);
            if (!tradeInsResult.IsSuccess)
            {
                return Result<UpdateSaleResponse>.Failure(tradeInsResult.Error);
            }

            saleTradeIns = tradeInsResult.Value;
        }

        try
        {
            if (requestedStatus == SaleStatus.Paid)
            {
                foreach (var detail in groupedDetails)
                {
                    var stock = stockMap[detail.ProductId];
                    stock.Reserve(detail.Quantity);
                    await _stockMovementRepository.AddAsync(
                        StockMovement.Create(
                            companyId,
                            sale.BranchId,
                            stock.ProductId,
                            stock.Id,
                            StockMovementType.Reserve,
                            detail.Quantity,
                            "Sale",
                            sale.Id.Value,
                            "Stock reserved for sale.",
                            _currentUserService.UserId),
                        cancellationToken);
                }

                sale.Update(customer?.Id, SaleStatus.OnHold, request.HasDelivery, saleDetails, salePayments, saleTradeIns, allowOverpayment: true, noDeliverySurchargeTotal: request.NoDeliverySurchargeTotal ?? 0);

                var cashAmount = sale.GetPaymentAmount(SalePaymentMethod.Cash);
                CashSession? session = null;

                if (cashAmount > 0)
                {
                    if (_currentUserService.UserId is null || request.CashDrawerId is null)
                    {
                        return Result<UpdateSaleResponse>.Failure(UpdateSaleErrors.CashDrawerRequired);
                    }

                    session = await _cashSessionRepository.GetOpenForBranchAsync(
                        sale.BranchId,
                        new CashDrawerId(request.CashDrawerId.Value),
                        companyId,
                        cancellationToken);

                    if (session is null)
                    {
                        return Result<UpdateSaleResponse>.Failure(UpdateSaleErrors.CashSessionRequired);
                    }
                }

                sale.MarkAsPaid(session?.Id);

                if (cashAmount > 0)
                {
                    session!.RegisterSaleIncome(cashAmount, sale.Id.Value, _currentUserService.UserId!);
                }

                foreach (var detail in groupedDetails)
                {
                    var stock = stockMap[detail.ProductId];
                    stock.ConfirmSaleOut(detail.Quantity);
                    await _stockMovementRepository.AddAsync(
                        StockMovement.Create(
                            companyId,
                            sale.BranchId,
                            stock.ProductId,
                            stock.Id,
                            StockMovementType.SaleOut,
                            detail.Quantity,
                            "Sale",
                            sale.Id.Value,
                            "Stock confirmed as sold.",
                            _currentUserService.UserId),
                        cancellationToken);
                }

                foreach (var tradeIn in sale.TradeIns)
                {
                    var stock = await _branchProductStockRepository.GetOrCreateAsync(
                        sale.BranchId,
                        tradeIn.ProductId,
                        companyId,
                        cancellationToken);

                    stock.ApplyManualEntry(tradeIn.Quantity);
                    await _stockMovementRepository.AddAsync(
                        StockMovement.Create(
                            companyId,
                            sale.BranchId,
                            stock.ProductId,
                            stock.Id,
                            StockMovementType.TradeInIn,
                            tradeIn.Quantity,
                            "Sale",
                            sale.Id.Value,
                            "Stock received from product trade-in.",
                            _currentUserService.UserId),
                        cancellationToken);
                }
            }
            else if (requestedStatus == SaleStatus.Cancel)
            {
                var currentDetailsSnapshot = currentGroupedDetails.Select(detail =>
                    SaleDetail.Create(new ProductId(detail.ProductId), detail.Quantity, detail.UnitPrice)).ToList();

                sale.Update(
                    customer?.Id,
                    requestedStatus,
                    request.HasDelivery,
                    currentDetailsSnapshot,
                    sale.Payments.ToList(),
                    sale.TradeIns.ToList());
            }
            else
            {
                foreach (var detail in groupedDetails)
                {
                    var stock = stockMap[detail.ProductId];
                    stock.Reserve(detail.Quantity);
                    await _stockMovementRepository.AddAsync(
                        StockMovement.Create(
                            companyId,
                            sale.BranchId,
                            stock.ProductId,
                            stock.Id,
                            StockMovementType.Reserve,
                            detail.Quantity,
                            "Sale",
                            sale.Id.Value,
                            "Stock reserved for sale.",
                            _currentUserService.UserId),
                        cancellationToken);
                }

                sale.Update(customer?.Id, requestedStatus, request.HasDelivery, saleDetails, salePayments, saleTradeIns, noDeliverySurchargeTotal: request.NoDeliverySurchargeTotal ?? 0);
            }

            if (requestedStatus == SaleStatus.Cancel && existingTransportAssignmentId is not null)
            {
                var assignment = await _saleTransportAssignmentRepository.GetByIdAsync(
                    existingTransportAssignmentId,
                    companyId,
                    cancellationToken);

                if (assignment is not null && assignment.Status != SaleTransportStatus.Delivered)
                {
                    assignment.Cancel();
                }
            }
        }
        catch (ArgumentException ex)
        {
            return Result<UpdateSaleResponse>.Failure(
                Error.Validation("Sales.Update.InvalidInput", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result<UpdateSaleResponse>.Failure(
                Error.Conflict("Sales.Update.NotEditable", ex.Message));
        }

        sale.SetSourceChannel(request.SourceChannel);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UpdateSaleResponse>.Success(
            new UpdateSaleResponse(
                sale.Id.Value,
                sale.BranchId.Value,
                sale.CustomerId?.Value,
                customer?.FullName,
                customer is null ? null : BuildCustomerDocument(customer),
                customer?.TaxId,
                sale.CashSessionId?.Value,
                sale.HasDelivery,
                sale.TransportAssignmentId?.Value,
                (int)sale.SaleStatus,
                sale.SaleStatus.ToString(),
                sale.NoDeliverySurchargeTotal,
                sale.TotalAmount,
                sale.MonetaryPaidAmount,
                sale.TradeInAmount,
                sale.SettledAmount,
                sale.PendingAmount,
                sale.ChangeAmount,
                sale.CreatedAt,
                sale.PaidAt,
                sale.UpdatedAt,
                sale.IsModified,
                sale.Details.Select(detail => new UpdateSaleDetailItemResponse(
                    detail.ProductId.Value,
                    GetProductName(productMap, detail.ProductId.Value),
                    GetProductBrand(productMap, detail.ProductId.Value),
                    detail.Quantity,
                    detail.UnitPrice,
                    detail.TotalAmount)).ToList(),
                sale.Payments.Select(payment => new UpdateSalePaymentItemResponse(
                    (int)payment.Method,
                    payment.Method.ToString(),
                    payment.Amount,
                    payment.Reference)).ToList(),
                sale.TradeIns.Select(tradeIn => new UpdateSaleTradeInItemResponse(
                    tradeIn.ProductId.Value,
                    GetProductName(productMap, tradeIn.ProductId.Value),
                    GetProductBrand(productMap, tradeIn.ProductId.Value),
                    tradeIn.Quantity,
                    tradeIn.Amount)).ToList()));
    }

    private static string? BuildCustomerDocument(Customer customer)
    {
        return customer.DocumentType is null || string.IsNullOrWhiteSpace(customer.DocumentNumber)
            ? null
            : $"{customer.DocumentType} {customer.DocumentNumber}";
    }

    private static string GetProductName(IDictionary<Guid, Product> productMap, Guid productId)
    {
        return productMap.TryGetValue(productId, out var product)
            ? product.Name
            : "Deleted product";
    }

    private static string GetProductBrand(IDictionary<Guid, Product> productMap, Guid productId)
    {
        return productMap.TryGetValue(productId, out var product)
            ? product.Brand
            : "Unknown";
    }

    private static List<SalePayment> BuildPayments(IReadOnlyList<UpdateSalePaymentItemRequest> paymentRequests)
    {
        return paymentRequests
            .GroupBy(payment => payment.IdPaymentMethod)
            .Select(group =>
            {
                if (!Enum.IsDefined(typeof(SalePaymentMethod), group.Key))
                {
                    throw new ArgumentException($"The payment method '{group.Key}' is invalid.");
                }

                var method = (SalePaymentMethod)group.Key;
                var amount = group.Sum(item => item.Amount);
                var reference = group.Select(item => item.Reference).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                return SalePayment.Create(method, amount, reference);
            })
            .ToList();
    }

    private async Task<Result<List<SaleTradeIn>>> BuildTradeInsAsync(
        IReadOnlyList<UpdateSaleTradeInItemRequest> tradeInRequests,
        IDictionary<Guid, Product> productMap,
        CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var groupedTradeIns = tradeInRequests
            .GroupBy(tradeIn => tradeIn.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                Amount = group.Sum(item => item.Amount)
            })
            .ToList();

        var tradeIns = new List<SaleTradeIn>();

        foreach (var tradeIn in groupedTradeIns)
        {
            var product = await _productRepository.GetByIdAsync(
                new ProductId(tradeIn.ProductId),
                companyId,
                cancellationToken);

            if (product is null)
            {
                return Result<List<SaleTradeIn>>.Failure(
                    Error.NotFound("Sales.Update.TradeInProductNotFound", $"The trade-in product '{tradeIn.ProductId}' was not found."));
            }

            if (!product.AllowsManualValueInSale)
            {
                return Result<List<SaleTradeIn>>.Failure(
                    Error.Validation(
                        "Sales.Update.TradeInManualValueNotAllowed",
                        $"The product '{product.Name}' does not allow manual value in sale and cannot be used as a trade-in."));
            }

            productMap[product.Id.Value] = product;
            tradeIns.Add(SaleTradeIn.Create(product.Id, tradeIn.Quantity, tradeIn.Amount));
        }

        return Result<List<SaleTradeIn>>.Success(tradeIns);
    }
}
