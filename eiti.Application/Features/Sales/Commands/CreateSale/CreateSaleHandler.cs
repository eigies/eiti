using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CreateSale;

public sealed class CreateSaleHandler : IRequestHandler<CreateSaleCommand, Result<CreateSaleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSaleHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ISaleRepository saleRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _saleRepository = saleRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateSaleResponse>> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var companyId = _currentUserService.CompanyId;
        if (!_currentUserService.IsAuthenticated || companyId is null)
        {
            return Result<CreateSaleResponse>.Failure(
                Error.Unauthorized("Sales.Create.Unauthorized", "The current user is not authenticated."));
        }

        if (!Enum.IsDefined(typeof(SaleStatus), request.IdSaleStatus))
        {
            return Result<CreateSaleResponse>.Failure(
                Error.Validation("Sales.Create.InvalidStatus", "The selected sale status is invalid."));
        }

        var requestedStatus = (SaleStatus)request.IdSaleStatus;

        if (requestedStatus == SaleStatus.Paid && !_currentUserService.HasPermission(PermissionCodes.SalesPay))
        {
            return Result<CreateSaleResponse>.Failure(
                Error.Forbidden("Sales.Create.PaymentForbidden", "The current user does not have permission to charge sales."));
        }

        if (requestedStatus == SaleStatus.Cancel)
        {
            return Result<CreateSaleResponse>.Failure(
                Error.Validation("Sales.Create.CancelNotAllowed", "A sale cannot be created with Cancel status."));
        }

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), companyId, cancellationToken);
        if (branch is null)
        {
            return Result<CreateSaleResponse>.Failure(
                Error.NotFound("Sales.Create.BranchNotFound", "The requested branch was not found."));
        }

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _customerRepository.GetByIdAsync(new CustomerId(request.CustomerId.Value), companyId, cancellationToken);
            if (customer is null)
            {
                return Result<CreateSaleResponse>.Failure(
                    Error.NotFound("Sales.Create.CustomerNotFound", "The selected customer was not found."));
            }
        }

        var groupedDetails = request.Details
            .GroupBy(detail => detail.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToList();

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
                return Result<CreateSaleResponse>.Failure(
                    Error.NotFound("Sales.Create.ProductNotFound", $"The product '{detail.ProductId}' was not found."));
            }

            productMap[product.Id.Value] = product;
            var stock = await _branchProductStockRepository.GetOrCreateAsync(
                branch.Id,
                product.Id,
                companyId,
                cancellationToken);

            stockMap[product.Id.Value] = stock;
            saleDetails.Add(SaleDetail.Create(product.Id, detail.Quantity, product.Price));
        }

        foreach (var detail in groupedDetails)
        {
            var stock = stockMap[detail.ProductId];

            try
            {
                stock.Reserve(detail.Quantity);
            }
            catch (ArgumentException ex)
            {
                return Result<CreateSaleResponse>.Failure(
                    Error.Validation("Sales.Create.InvalidQuantity", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Result<CreateSaleResponse>.Failure(
                    Error.Conflict("Sales.Create.StockUnavailable", ex.Message));
            }

            await _stockMovementRepository.AddAsync(
                StockMovement.Create(
                    companyId,
                    branch.Id,
                    stock.ProductId,
                    stock.Id,
                    StockMovementType.Reserve,
                    detail.Quantity,
                    "Sale",
                    null,
                    "Stock reserved for sale.",
                    _currentUserService.UserId),
                cancellationToken);
        }

        List<SalePayment> salePayments;
        try
        {
            salePayments = BuildPayments(request.Payments);
        }
        catch (ArgumentException ex)
        {
            return Result<CreateSaleResponse>.Failure(
                Error.Validation("Sales.Create.InvalidPayments", ex.Message));
        }

        var tradeInsResult = await BuildTradeInsAsync(request.TradeIns, productMap, companyId, cancellationToken);
        if (!tradeInsResult.IsSuccess)
        {
            return Result<CreateSaleResponse>.Failure(tradeInsResult.Error!);
        }

        var saleTradeIns = tradeInsResult.Value!;

        Sale sale;

        try
        {
            sale = Sale.Create(
                companyId,
                branch.Id,
                customer?.Id,
                request.HasDelivery,
                requestedStatus == SaleStatus.Paid ? SaleStatus.OnHold : requestedStatus,
                saleDetails,
                salePayments,
                saleTradeIns);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<CreateSaleResponse>.Failure(
                Error.Validation("Sales.Create.InvalidInput", ex.Message));
        }

        if (requestedStatus == SaleStatus.Paid)
        {
            var cashAmount = sale.GetPaymentAmount(SalePaymentMethod.Cash);
            CashSession? session = null;

            if (cashAmount > 0)
            {
                if (_currentUserService.UserId is null || request.CashDrawerId is null)
                {
                    return Result<CreateSaleResponse>.Failure(
                        Error.Validation("Sales.Create.CashDrawerRequired", "A cash drawer is required when cash amount is greater than zero."));
                }

                session = await _cashSessionRepository.GetOpenForBranchAsync(
                    branch.Id,
                    new CashDrawerId(request.CashDrawerId.Value),
                    companyId,
                    cancellationToken);

                if (session is null)
                {
                    return Result<CreateSaleResponse>.Failure(
                        Error.Conflict("Sales.Create.CashSessionRequired", "An open cash session is required for the selected cash drawer."));
                }
            }

            try
            {
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
                            branch.Id,
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
                        branch.Id,
                        tradeIn.ProductId,
                        companyId,
                        cancellationToken);

                    stock.ApplyManualEntry(tradeIn.Quantity);
                    await _stockMovementRepository.AddAsync(
                        StockMovement.Create(
                            companyId,
                            branch.Id,
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
            catch (InvalidOperationException ex)
            {
                return Result<CreateSaleResponse>.Failure(
                    Error.Conflict("Sales.Create.InvalidPaymentFlow", ex.Message));
            }
        }

        await _saleRepository.AddAsync(sale, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateSaleResponse>.Success(
            new CreateSaleResponse(
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
                sale.TotalAmount,
                sale.MonetaryPaidAmount,
                sale.TradeInAmount,
                sale.SettledAmount,
                sale.PendingAmount,
                sale.CreatedAt,
                sale.PaidAt,
                sale.UpdatedAt,
                sale.IsModified,
                sale.Details.Select(detail => new CreateSaleDetailItemResponse(
                    detail.ProductId.Value,
                    GetProductName(productMap, detail.ProductId.Value),
                    GetProductBrand(productMap, detail.ProductId.Value),
                    detail.Quantity,
                    detail.UnitPrice,
                    detail.TotalAmount)).ToList(),
                sale.Payments.Select(payment => new CreateSalePaymentItemResponse(
                    (int)payment.Method,
                    payment.Method.ToString(),
                    payment.Amount,
                    payment.Reference)).ToList(),
                sale.TradeIns.Select(tradeIn => new CreateSaleTradeInItemResponse(
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

    private static List<SalePayment> BuildPayments(IReadOnlyList<CreateSalePaymentItemRequest> paymentRequests)
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
        IReadOnlyList<CreateSaleTradeInItemRequest> tradeInRequests,
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
                    Error.NotFound("Sales.Create.TradeInProductNotFound", $"The trade-in product '{tradeIn.ProductId}' was not found."));
            }

            productMap[product.Id.Value] = product;
            tradeIns.Add(SaleTradeIn.Create(product.Id, tradeIn.Quantity, tradeIn.Amount));
        }

        return Result<List<SaleTradeIn>>.Success(tradeIns);
    }
}
