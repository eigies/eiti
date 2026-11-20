using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Application.Features.Sales.Common;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CreateCcSale;

public sealed class CreateCcSaleHandler : IRequestHandler<CreateCcSaleCommand, Result<CreateCcSaleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleInvoicingService _saleInvoicingService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCcSaleHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ISaleRepository saleRepository,
        ISaleInvoicingService saleInvoicingService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _saleRepository = saleRepository;
        _saleInvoicingService = saleInvoicingService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateCcSaleResponse>> Handle(CreateCcSaleCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CreateCcSaleResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;
        if (companyId is null)
        {
            return Result<CreateCcSaleResponse>.Failure(CreateCcSaleErrors.Unauthorized);
        }

        var branchAccess = _currentUserService.EnsureBranchAccess(request.BranchId);
        if (branchAccess.IsFailure)
            return Result<CreateCcSaleResponse>.Failure(branchAccess.Error);

        // Igual que en mostrador: si este intento de alta ya creó la venta, se devuelve esa en vez de crear otra.
        if (request.ClientRequestId is Guid clientRequestId)
        {
            var existing = await _saleRepository.GetByClientRequestIdAsync(companyId, clientRequestId, cancellationToken);
            if (existing is not null)
            {
                return Result<CreateCcSaleResponse>.Success(await BuildExistingSaleResponseAsync(existing, companyId, cancellationToken));
            }
        }

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), companyId, cancellationToken);
        if (branch is null)
        {
            return Result<CreateCcSaleResponse>.Failure(CreateCcSaleErrors.BranchNotFound);
        }

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(request.CustomerId), companyId, cancellationToken);
        if (customer is null)
        {
            return Result<CreateCcSaleResponse>.Failure(CreateCcSaleErrors.CustomerNotFound);
        }

        // Mismo criterio que mostrador: si se pidió factura y el cliente no la permite, se corta
        // antes de guardar. La facturación automática no bloquea: el rechazo queda en la venta.
        if (request.RequestInvoicing && _saleInvoicingService.IsEnabled)
        {
            var branchError = SaleInvoicingBranchRules.Validate(branch);
            if (branchError is not null)
            {
                return Result<CreateCcSaleResponse>.Failure(CreateCcSaleErrors.BranchWithoutPointOfSale(branchError));
            }

            var receiverError = SaleInvoicingReceiverRules.Validate(customer, request.InvoiceLetter);
            if (receiverError is not null)
            {
                return Result<CreateCcSaleResponse>.Failure(CreateCcSaleErrors.InvoicingReceiverInvalid(receiverError));
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
                return Result<CreateCcSaleResponse>.Failure(
                    Error.NotFound("Sales.CreateCc.ProductNotFound", $"The product '{detail.ProductId}' was not found."));
            }

            productMap[product.Id.Value] = product;
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

        // Canjes: mismas reglas que la venta normal (el producto debe permitir valor manual). El valor del
        // canje saldará parte de la deuda CC; el ingreso de stock (TradeInIn) se registra más abajo.
        var groupedTradeIns = (request.TradeIns ?? [])
            .GroupBy(tradeIn => tradeIn.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                Amount = group.Sum(item => item.Amount)
            })
            .ToList();

        var saleTradeIns = new List<SaleTradeIn>();
        foreach (var tradeIn in groupedTradeIns)
        {
            if (!productMap.TryGetValue(tradeIn.ProductId, out var tradeInProduct))
            {
                tradeInProduct = await _productRepository.GetByIdAsync(
                    new ProductId(tradeIn.ProductId), companyId, cancellationToken);

                if (tradeInProduct is null)
                {
                    return Result<CreateCcSaleResponse>.Failure(
                        Error.NotFound("Sales.CreateCc.TradeInProductNotFound", $"The trade-in product '{tradeIn.ProductId}' was not found."));
                }

                productMap[tradeInProduct.Id.Value] = tradeInProduct;
            }

            if (!tradeInProduct.AllowsManualValueInSale)
            {
                return Result<CreateCcSaleResponse>.Failure(
                    Error.Validation(
                        "Sales.CreateCc.TradeInManualValueNotAllowed",
                        $"The product '{tradeInProduct.Name}' does not allow manual value in sale and cannot be used as a trade-in."));
            }

            saleTradeIns.Add(SaleTradeIn.Create(tradeInProduct.Id, tradeIn.Quantity, tradeIn.Amount));
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
                return Result<CreateCcSaleResponse>.Failure(
                    Error.Validation("Sales.CreateCc.InvalidQuantity", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Result<CreateCcSaleResponse>.Failure(
                    Error.Conflict("Sales.CreateCc.StockUnavailable", ex.Message));
            }
        }

        var branchSaleCount = await _saleRepository.CountByBranchAsync(branch.Id, cancellationToken);
        var codePrefix = !string.IsNullOrWhiteSpace(branch.Code)
            ? branch.Code.ToUpper()
            : branch.Name.ToUpper()[..Math.Min(3, branch.Name.Length)];
        var saleCode = $"{codePrefix}-{(branchSaleCount + 1).ToString().PadLeft(3, '0')}";

        Sale sale;

        try
        {
            sale = Sale.CreateCc(
                companyId,
                branch.Id,
                customer.Id,
                saleDetails,
                tradeIns: saleTradeIns,
                code: saleCode,
                generalDiscountPercent: request.GeneralDiscountPercent);
            sale.SetNotes(request.Notes);
            // La CC no tiene tope, pero el descuento queda trazado igual para el reporte de descuentos.
            sale.TrackGeneralDiscount(previousPercent: 0m, _currentUserService.UserId?.Value, reason: null);
            if (request.ClientRequestId is Guid requestId)
            {
                sale.AssignClientRequestId(requestId);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<CreateCcSaleResponse>.Failure(
                Error.Validation("Sales.CreateCc.InvalidInput", ex.Message));
        }

        if (request.ManualOverridePrice.HasValue)
        {
            sale.SetManualOverride(request.ManualOverridePrice.Value, _currentUserService.UserId?.Value);
        }

        // Desglose de IVA (venta proveniente de un presupuesto convertido "con IVA"): los detalles ya vienen
        // con IVA incluido, así que VatAmount se deriva del total final (respeta descuentos/override).
        if (request.VatRate.HasValue)
        {
            sale.SetVat(request.VatRate.Value);
        }

        // Movimientos de reserva: se registran ahora que la venta existe, para quedar atados a su código.
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
                    "Stock reserved for CC sale.",
                    _currentUserService.UserId),
                cancellationToken);
        }

        // Ingreso de stock por canje: la batería usada entra al inventario al momento de la venta,
        // igual que en la venta normal (movimiento TradeInIn).
        foreach (var tradeIn in sale.TradeIns)
        {
            var tradeInStock = await _branchProductStockRepository.GetOrCreateAsync(
                branch.Id, tradeIn.ProductId, companyId, cancellationToken);

            tradeInStock.ApplyManualEntry(tradeIn.Quantity);
            await _stockMovementRepository.AddAsync(
                StockMovement.Create(
                    companyId,
                    branch.Id,
                    tradeInStock.ProductId,
                    tradeInStock.Id,
                    StockMovementType.TradeInIn,
                    tradeIn.Quantity,
                    "Sale",
                    sale.Id.Value,
                    "Stock received from product trade-in (CC sale).",
                    _currentUserService.UserId),
                cancellationToken);
        }

        await _saleRepository.AddAsync(sale, cancellationToken);

        decimal creditApplied = 0m;
        if (customer.CreditBalance > 0 && sale.CcPendingAmount > 0)
        {
            creditApplied = Math.Min(customer.CreditBalance, sale.CcPendingAmount);
            customer.ConsumeCredit(creditApplied);
            sale.AddCcPayment(
                SalePaymentMethod.CustomerCredit,
                creditApplied,
                DateTime.UtcNow,
                "Saldo a favor aplicado automáticamente",
                allowOverpayment: false);
            _customerRepository.Update(customer);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // La venta ya existe: facturar no se corta aunque el navegador abandone el pedido (ver CreateSaleHandler).
        var invoicing = await SaleInvoicingFlow.TryInvoiceAsync(
            _saleInvoicingService, _unitOfWork, sale, request.RequestInvoicing, CancellationToken.None);

        return Result<CreateCcSaleResponse>.Success(BuildResponse(sale, customer, productMap, creditApplied, invoicing));
    }

    // Reintento de un pedido que ya creó la venta: la misma respuesta que el alta, armada con lo guardado.
    private async Task<CreateCcSaleResponse> BuildExistingSaleResponseAsync(
        Sale sale,
        CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var customer = sale.CustomerId is null
            ? null
            : await _customerRepository.GetByIdAsync(sale.CustomerId, companyId, cancellationToken);

        var productIds = sale.Details.Select(detail => detail.ProductId)
            .Concat(sale.TradeIns.Select(tradeIn => tradeIn.ProductId))
            .Distinct()
            .ToList();
        var productMap = (await _productRepository.GetByIdsAsync(productIds, companyId, cancellationToken))
            .ToDictionary(product => product.Id.Value);

        // El saldo a favor que el alta aplicó queda como cobro CC con ese método.
        var creditApplied = sale.CcPayments
            .Where(payment => payment.Status == SaleCcPaymentStatus.Active && payment.Method == SalePaymentMethod.CustomerCredit)
            .Sum(payment => payment.Amount);

        var invoicing = _saleInvoicingService.IsEnabled
            ? await _saleInvoicingService.GetInvoiceOutcomeAsync(sale, cancellationToken)
            : null;

        return BuildResponse(sale, customer, productMap, creditApplied, invoicing);
    }

    private static CreateCcSaleResponse BuildResponse(
        Sale sale,
        Customer? customer,
        IDictionary<Guid, Product> productMap,
        decimal creditApplied,
        SaleInvoicingOutcome? invoicing)
    {
        return new CreateCcSaleResponse(
            sale.Id.Value,
            sale.Code,
            sale.BranchId.Value,
            // Toda venta CC nace con cliente (Sale.CreateCc lo exige); la ficha puede faltar en un reintento.
            customer?.Id.Value ?? sale.CustomerId?.Value ?? Guid.Empty,
            customer?.FullName,
            (int)sale.SaleStatus,
            sale.SaleStatus.ToString(),
            sale.GeneralDiscountPercent,
            sale.OriginalTotal,
            sale.TotalAmount,
            sale.VatRate,
            sale.VatAmount,
            sale.ManualOverridePrice,
            sale.IsCuentaCorriente,
            sale.CreatedAt,
            creditApplied,
            customer?.CreditBalance ?? 0m,
            sale.Details.Select(detail => new CreateCcSaleDetailItemResponse(
                detail.ProductId.Value,
                GetProductName(productMap, detail.ProductId.Value),
                GetProductBrand(productMap, detail.ProductId.Value),
                detail.Quantity,
                detail.UnitPrice,
                detail.DiscountPercent,
                detail.TotalAmount)).ToList(),
            sale.TradeInAmount,
            sale.CcPendingAmount,
            sale.TradeIns.Select(tradeIn => new CreateCcSaleTradeInItemResponse(
                tradeIn.ProductId.Value,
                GetProductName(productMap, tradeIn.ProductId.Value),
                GetProductBrand(productMap, tradeIn.ProductId.Value),
                tradeIn.Quantity,
                tradeIn.Amount)).ToList(),
            CreateSaleInvoicingResponse.From(invoicing));
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
}
