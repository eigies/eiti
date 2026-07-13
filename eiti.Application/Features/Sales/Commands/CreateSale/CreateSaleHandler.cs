using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Banks.Common;
using eiti.Domain.Banks;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
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
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IAddressRepository _addressRepository;
    private readonly IBankRepository _bankRepository;
    private readonly IChequeRepository _chequeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSaleHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ISaleRepository saleRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IAddressRepository addressRepository,
        IBankRepository bankRepository,
        IChequeRepository chequeRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _saleRepository = saleRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _addressRepository = addressRepository;
        _bankRepository = bankRepository;
        _chequeRepository = chequeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateSaleResponse>> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CreateSaleResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;
        if (companyId is null)
        {
            return Result<CreateSaleResponse>.Failure(CreateSaleErrors.Unauthorized);
        }

        var effectiveCashDrawerId = await CashDrawerAccessPolicy.ResolveEffectiveDrawerIdAsync(
            _currentUserService,
            _cashDrawerRepository,
            request.CashDrawerId,
            cancellationToken);

        if (!Enum.IsDefined(typeof(SaleStatus), request.IdSaleStatus))
        {
            return Result<CreateSaleResponse>.Failure(CreateSaleErrors.InvalidStatus);
        }

        var requestedStatus = (SaleStatus)request.IdSaleStatus;

        if (requestedStatus == SaleStatus.Paid && !_currentUserService.HasPermission(PermissionCodes.SalesPay))
        {
            return Result<CreateSaleResponse>.Failure(CreateSaleErrors.PaymentForbidden);
        }

        if (requestedStatus == SaleStatus.Cancel)
        {
            return Result<CreateSaleResponse>.Failure(CreateSaleErrors.CancelNotAllowed);
        }

        var branchAccess = _currentUserService.EnsureBranchAccess(request.BranchId);
        if (branchAccess.IsFailure)
            return Result<CreateSaleResponse>.Failure(branchAccess.Error);

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), companyId, cancellationToken);
        if (branch is null)
        {
            return Result<CreateSaleResponse>.Failure(CreateSaleErrors.BranchNotFound);
        }

        if (effectiveCashDrawerId.HasValue)
        {
            var openSession = await _cashSessionRepository.GetOpenForBranchAsync(
                branch.Id,
                new CashDrawerId(effectiveCashDrawerId.Value),
                companyId,
                cancellationToken);

            if (openSession is not null && BusinessDay.IsFromPreviousBusinessDay(openSession.OpenedAt))
            {
                return Result<CreateSaleResponse>.Failure(CreateSaleErrors.CashSessionFromPreviousDay);
            }
        }

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _customerRepository.GetByIdAsync(new CustomerId(request.CustomerId.Value), companyId, cancellationToken);
            if (customer is null)
            {
                return Result<CreateSaleResponse>.Failure(CreateSaleErrors.CustomerNotFound);
            }
        }

        var groupedDetails = request.Details
            .GroupBy(detail => detail.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                UnitPrice = group.FirstOrDefault(i => i.UnitPrice.HasValue)?.UnitPrice,
                DiscountPercent = group.First().DiscountPercent
            })
            .ToList();

        // Pre-fetch de todos los productos (detalles + trade-ins) en una sola query (evita N+1).
        var allProductIds = groupedDetails.Select(d => d.ProductId)
            .Concat(request.TradeIns.Select(t => t.ProductId))
            .Distinct()
            .Select(id => new ProductId(id))
            .ToList();
        var productMap = (await _productRepository.GetByIdsAsync(allProductIds, companyId, cancellationToken))
            .ToDictionary(p => p.Id.Value);

        var saleDetails = new List<SaleDetail>();
        var stockMap = new Dictionary<Guid, BranchProductStock>();

        foreach (var detail in groupedDetails)
        {
            if (!productMap.TryGetValue(detail.ProductId, out var product))
            {
                return Result<CreateSaleResponse>.Failure(
                    Error.NotFound("Sales.Create.ProductNotFound", $"The product '{detail.ProductId}' was not found."));
            }

            var stock = await _branchProductStockRepository.GetOrCreateAsync(
                branch.Id,
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
                unitPrice = BranchPricing.ResolvePrice(stock, product);
            }
            saleDetails.Add(SaleDetail.Create(product.Id, detail.Quantity, unitPrice, detail.DiscountPercent, BranchPricing.ResolveCost(stock, product)));
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

        var tradeInsResult = BuildTradeIns(request.TradeIns, productMap);
        if (!tradeInsResult.IsSuccess)
        {
            return Result<CreateSaleResponse>.Failure(tradeInsResult.Error!);
        }

        var saleTradeIns = tradeInsResult.Value!;

        var branchSaleCount = await _saleRepository.CountByBranchAsync(branch.Id, cancellationToken);
        var codePrefix = !string.IsNullOrWhiteSpace(branch.Code)
            ? branch.Code.ToUpper()
            : branch.Name.ToUpper()[..Math.Min(3, branch.Name.Length)];
        var saleCode = $"{codePrefix}-{(branchSaleCount + 1).ToString().PadLeft(3, '0')}";

        // Card surcharge is applied to the sale subtotal (not back-computed from payment amount).
        // Surcharge = saleSubtotal * plan.SurchargePct / 100, computed once for the sale.
        var itemsSubtotal = saleDetails.Sum(d => d.TotalAmount) + (request.NoDeliverySurchargeTotal ?? 0m);
        if (request.GeneralDiscountPercent > 0)
            itemsSubtotal = itemsSubtotal * (1m - request.GeneralDiscountPercent / 100m);

        // Pre-fetch de los bancos de tarjeta usados en los pagos (1 query, evita N+1 y el doble fetch
        // entre el cálculo de recargo y el set de datos de tarjeta).
        var cardBankIds = request.Payments
            .Where(p => (SalePaymentMethod)p.IdPaymentMethod == SalePaymentMethod.Card && p.CardBankId.HasValue)
            .Select(p => p.CardBankId!.Value)
            .Distinct()
            .ToList();
        var transferBankIds = request.Payments
            .Where(p => (SalePaymentMethod)p.IdPaymentMethod == SalePaymentMethod.Transfer && p.TransferBankId.HasValue)
            .Select(p => p.TransferBankId!.Value)
            .Distinct()
            .ToList();
        var chequeBankIds = request.Payments
            .Where(p => (SalePaymentMethod)p.IdPaymentMethod == SalePaymentMethod.Check && p.Cheque is not null)
            .Select(p => p.Cheque!.BankId)
            .Distinct()
            .ToList();
        var allBankIds = cardBankIds
            .Concat(transferBankIds)
            .Concat(chequeBankIds)
            .Distinct()
            .ToList();
        var bankMap = allBankIds.Count == 0
            ? new Dictionary<int, Bank>()
            : (await _bankRepository.GetByIdsAsync(allBankIds, companyId, cancellationToken))
                .ToDictionary(b => b.Id);

        if (cardBankIds.Any(id => !bankMap.TryGetValue(id, out var bank) || !BankUsageRules.Supports(bank, BankUsage.Card)))
        {
            return Result<CreateSaleResponse>.Failure(CreateSaleErrors.CardBankInvalid);
        }

        if (transferBankIds.Any(id => !bankMap.TryGetValue(id, out var bank) || !BankUsageRules.Supports(bank, BankUsage.Transfer)))
        {
            return Result<CreateSaleResponse>.Failure(CreateSaleErrors.TransferBankInvalid);
        }

        if (chequeBankIds.Any(id => !bankMap.TryGetValue(id, out var bank) || !BankUsageRules.Supports(bank, BankUsage.Cheque)))
        {
            return Result<CreateSaleResponse>.Failure(CreateSaleErrors.ChequeBankInvalid);
        }

        var cardSurchargeTotal = 0m;
        foreach (var reqPayment in request.Payments)
        {
            if ((SalePaymentMethod)reqPayment.IdPaymentMethod == SalePaymentMethod.Card
                && reqPayment.CardBankId.HasValue
                && reqPayment.CardCuotas.HasValue)
            {
                bankMap.TryGetValue(reqPayment.CardBankId.Value, out var bank);
                var plan = CardSurchargeCalculator.FindPlan(bank, reqPayment.CardCuotas.Value);
                if (plan is not null && plan.SurchargePct > 0)
                {
                    cardSurchargeTotal += CardSurchargeCalculator.Compute(itemsSubtotal, plan.SurchargePct);
                    break; // One surcharge applies to the sale, even with multiple card payments
                }
            }
        }

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
                saleTradeIns,
                allowOverpayment: requestedStatus == SaleStatus.Paid,
                noDeliverySurchargeTotal: request.NoDeliverySurchargeTotal ?? 0,
                code: saleCode,
                deliveryAddress: request.DeliveryAddress,
                generalDiscountPercent: request.GeneralDiscountPercent,
                cardSurchargeTotal: cardSurchargeTotal,
                contactPhone: request.ContactPhone);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<CreateSaleResponse>.Failure(
                Error.Validation("Sales.Create.InvalidInput", ex.Message));
        }

        sale.SetCashDrawer(effectiveCashDrawerId.HasValue ? new CashDrawerId(effectiveCashDrawerId.Value) : null);

        // Movimientos de reserva: se registran ahora que la venta existe, para quedar atados a su código
        // (antes se creaban con ReferenceId null y el movimiento no se podía mapear a la venta).
        foreach (var detail in groupedDetails)
        {
            var stock = stockMap[detail.ProductId];
            await _stockMovementRepository.AddAsync(
                StockMovement.Create(
                    companyId,
                    branch.Id,
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

        if (requestedStatus == SaleStatus.Paid)
        {
            var cashAmount = sale.GetPaymentAmount(SalePaymentMethod.Cash);
            var transferAmount = sale.GetPaymentAmount(SalePaymentMethod.Transfer);
            var cardAmount = sale.GetPaymentAmount(SalePaymentMethod.Card);
            CashSession? session = null;

            // Efectivo, transferencia y tarjeta generan un movimiento de ingreso en caja, por lo que
            // los tres exigen un cajón resuelto + sesión abierta. Sin esta validación, un cobro por
            // transferencia/tarjeta de un usuario sin cajón asignado se marcaba pagado pero no se
            // imputaba a ninguna caja (la venta quedaba "perdida" para el arqueo y los reportes).
            var requiresCashSession = cashAmount > 0 || transferAmount > 0 || cardAmount > 0;

            if (requiresCashSession)
            {
                if (_currentUserService.UserId is null || effectiveCashDrawerId is null)
                {
                    return Result<CreateSaleResponse>.Failure(CreateSaleErrors.CashDrawerRequired);
                }

                session = await _cashSessionRepository.GetOpenForBranchAsync(
                    branch.Id,
                    new CashDrawerId(effectiveCashDrawerId.Value),
                    companyId,
                    cancellationToken);

                if (session is null)
                {
                    return Result<CreateSaleResponse>.Failure(CreateSaleErrors.CashSessionRequired);
                }
            }

            try
            {
                sale.MarkAsPaid(
                    effectiveCashDrawerId.HasValue ? new CashDrawerId(effectiveCashDrawerId.Value) : null,
                    session?.Id);

                if (cashAmount > 0)
                {
                    session!.RegisterSaleIncome(cashAmount, sale.Id.Value, _currentUserService.UserId!);
                }

                if (transferAmount > 0)
                {
                    session!.RegisterTransferIncome(transferAmount, sale.Id.Value, _currentUserService.UserId!);
                }

                if (cardAmount > 0)
                {
                    session!.RegisterCardIncome(cardAmount, sale.Id.Value, _currentUserService.UserId!);
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

        sale.SetSourceChannel(request.SourceChannel);

        // Process card and cheque data for payments
        // BuildPayments groups by method, so use first matching request per method
        var paymentRequestByMethod = request.Payments
            .GroupBy(p => p.IdPaymentMethod)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var payment in sale.Payments)
        {
            var methodKey = (int)payment.Method;
            if (!paymentRequestByMethod.TryGetValue(methodKey, out var reqLine))
                continue;

            if (payment.Method == SalePaymentMethod.Card
                && reqLine.CardBankId.HasValue
                && reqLine.CardCuotas.HasValue
                && bankMap.TryGetValue(reqLine.CardBankId.Value, out var bank))
            {
                var plan = CardSurchargeCalculator.FindPlan(bank, reqLine.CardCuotas.Value);
                if (plan is not null)
                {
                    payment.SetCardData(bank.Id, plan.Cuotas, plan.SurchargePct, cardSurchargeTotal);
                }
            }

            if (payment.Method == SalePaymentMethod.Transfer && reqLine.TransferBankId.HasValue)
            {
                payment.SetTransferBank(reqLine.TransferBankId.Value);
            }

            if (payment.Method == SalePaymentMethod.Check && reqLine.Cheque is not null)
            {
                var chequeData = reqLine.Cheque;
                var cheque = Cheque.CreateForRegularSale(
                    companyId,
                    sale.Id.Value,
                    (int)payment.Method,
                    chequeData.BankId,
                    chequeData.Numero,
                    chequeData.Titular,
                    chequeData.CuitDni,
                    chequeData.Monto,
                    chequeData.FechaEmision,
                    chequeData.FechaVencimiento,
                    chequeData.Notas);
                await _chequeRepository.AddAsync(cheque, cancellationToken);
            }
        }

        await _saleRepository.AddAsync(sale, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var customerAddress = await BuildCustomerAddress(customer, cancellationToken);

        return Result<CreateSaleResponse>.Success(
            new CreateSaleResponse(
                sale.Id.Value,
                sale.Code,
                sale.BranchId.Value,
                sale.CustomerId?.Value,
                customer?.FullName,
                customer is null ? null : BuildCustomerDocument(customer),
                customer?.TaxId,
                customerAddress,
                sale.DeliveryAddress,
                sale.CashDrawerId?.Value,
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
                sale.Details.Select(detail => new CreateSaleDetailItemResponse(
                    detail.ProductId.Value,
                    GetProductName(productMap, detail.ProductId.Value),
                    GetProductBrand(productMap, detail.ProductId.Value),
                    detail.Quantity,
                    detail.UnitPrice,
                    detail.DiscountPercent,
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

    private async Task<string?> BuildCustomerAddress(Customer? customer, CancellationToken cancellationToken)
    {
        if (customer?.AddressId is null)
            return null;

        var address = await _addressRepository.GetByIdAsync(customer.AddressId, cancellationToken);
        if (address is null)
            return null;

        return FormatAddress(address);
    }

    private static string FormatAddress(Domain.Addresses.Address address)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(address.Street))
        {
            var street = address.Street;
            if (!string.IsNullOrWhiteSpace(address.StreetNumber))
                street += $" {address.StreetNumber}";
            if (!string.IsNullOrWhiteSpace(address.Floor))
                street += $", Piso {address.Floor}";
            if (!string.IsNullOrWhiteSpace(address.Apartment))
                street += $", Depto {address.Apartment}";
            parts.Add(street);
        }

        if (!string.IsNullOrWhiteSpace(address.City))
            parts.Add(address.City);

        if (!string.IsNullOrWhiteSpace(address.StateOrProvince))
            parts.Add(address.StateOrProvince);

        return parts.Count > 0 ? string.Join(", ", parts) : null;
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

    // Los productos de trade-in ya vienen en productMap (pre-fetch único arriba) — sin queries por ítem.
    private static Result<List<SaleTradeIn>> BuildTradeIns(
        IReadOnlyList<CreateSaleTradeInItemRequest> tradeInRequests,
        IReadOnlyDictionary<Guid, Product> productMap)
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
            if (!productMap.TryGetValue(tradeIn.ProductId, out var product))
            {
                return Result<List<SaleTradeIn>>.Failure(
                    Error.NotFound("Sales.Create.TradeInProductNotFound", $"The trade-in product '{tradeIn.ProductId}' was not found."));
            }

            if (!product.AllowsManualValueInSale)
            {
                return Result<List<SaleTradeIn>>.Failure(
                    Error.Validation(
                        "Sales.Create.TradeInManualValueNotAllowed",
                        $"The product '{product.Name}' does not allow manual value in sale and cannot be used as a trade-in."));
            }

            tradeIns.Add(SaleTradeIn.Create(product.Id, tradeIn.Quantity, tradeIn.Amount));
        }

        return Result<List<SaleTradeIn>>.Success(tradeIns);
    }
}
