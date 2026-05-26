using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Products;
using eiti.Domain.Purchases;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Purchases.Commands.CreatePurchase;

public sealed class CreatePurchaseHandler : IRequestHandler<CreatePurchaseCommand, Result<CreatePurchaseResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IProductRepository _productRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePurchaseHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        IProductRepository productRepository,
        IPurchaseRepository purchaseRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _productRepository = productRepository;
        _purchaseRepository = purchaseRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreatePurchaseResponse>> Handle(CreatePurchaseCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<CreatePurchaseResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        // Validate supplier if provided
        string? supplierName = null;
        if (command.SupplierId.HasValue)
        {
            var supplier = await _supplierRepository.GetByIdAsync(command.SupplierId.Value, companyId.Value, cancellationToken);
            if (supplier is null)
                return Result<CreatePurchaseResponse>.Failure(CreatePurchaseErrors.SupplierNotFound);
            supplierName = supplier.Name;
        }

        // Validate duplicate invoice number (per company + supplier, ignoring cancelled)
        if (!string.IsNullOrWhiteSpace(command.InvoiceNumber))
        {
            var duplicate = await _purchaseRepository.ExistsWithInvoiceNumberAsync(
                companyId.Value, command.SupplierId, command.InvoiceNumber.Trim(), cancellationToken);
            if (duplicate)
                return Result<CreatePurchaseResponse>.Failure(CreatePurchaseErrors.DuplicateInvoiceNumber);
        }

        // Validate payment methods
        foreach (var payment in command.Payments)
        {
            if (!Enum.IsDefined(typeof(PurchasePaymentMethod), payment.Method))
                return Result<CreatePurchaseResponse>.Failure(CreatePurchaseErrors.InvalidPaymentMethod);
        }

        // Build purchase details, fetching products
        var details = new List<PurchaseDetail>();
        var productMap = new Dictionary<Guid, Product>();

        foreach (var detailRequest in command.Details)
        {
            var product = await _productRepository.GetByIdAsync(
                new ProductId(detailRequest.ProductId),
                companyId,
                cancellationToken);

            if (product is null)
                return Result<CreatePurchaseResponse>.Failure(CreatePurchaseErrors.ProductNotFound(detailRequest.ProductId));

            productMap[product.Id.Value] = product;
            details.Add(PurchaseDetail.Create(product.Id.Value, product.Name, detailRequest.Quantity, detailRequest.UnitCost));
        }

        // Generate purchase code (company-wide sequential)
        var purchaseCount = await _purchaseRepository.CountAsync(companyId.Value, null, null, null, null, cancellationToken);
        var purchaseCode = $"COMP-{(purchaseCount + 1).ToString().PadLeft(4, '0')}";

        // Create purchase aggregate
        var purchase = Purchase.Create(
            companyId.Value,
            command.BranchId,
            command.SupplierId,
            details,
            command.InvoiceNumber,
            command.Notes,
            userId.Value,
            purchaseCode,
            command.IvaPct,
            command.IngresosBrutosPct);

        // Update stock for each detail and create stock movements
        foreach (var detail in purchase.Details)
        {
            var stock = await _branchProductStockRepository.GetOrCreateAsync(
                new BranchId(command.BranchId),
                new ProductId(detail.ProductId),
                companyId,
                cancellationToken);

            try
            {
                stock.ApplyManualEntry((int)detail.Quantity);
            }
            catch (ArgumentException ex)
            {
                return Result<CreatePurchaseResponse>.Failure(
                    Error.Validation("Purchases.Create.InvalidQuantity", ex.Message));
            }

            await _stockMovementRepository.AddAsync(
                StockMovement.Create(
                    companyId,
                    new BranchId(command.BranchId),
                    stock.ProductId,
                    stock.Id,
                    StockMovementType.PurchaseIn,
                    (int)detail.Quantity,
                    "Purchase",
                    purchase.Id,
                    "Stock received from purchase.",
                    userId),
                cancellationToken);
        }

        // Always prefer the user's assigned drawer; fall back to any open session only for users with CashDrawerViewAll and no assigned drawer.
        CashSession? openSession = null;

        if (command.Payments.Count > 0)
        {
            var assignedDrawer = await _cashDrawerRepository.GetByAssignedUserAsync(userId, companyId, cancellationToken);

            if (assignedDrawer is not null)
            {
                openSession = await _cashSessionRepository.GetOpenByDrawerAsync(assignedDrawer.Id, companyId, cancellationToken);
                if (openSession is null)
                    return Result<CreatePurchaseResponse>.Failure(CreatePurchaseErrors.NoCashSessionOpen);
            }
            else if (_currentUserService.HasPermission(PermissionCodes.CashDrawerViewAll))
            {
                openSession = await _cashSessionRepository.GetAnyOpenByCompanyAsync(companyId, cancellationToken);
                if (openSession is null)
                    return Result<CreatePurchaseResponse>.Failure(CreatePurchaseErrors.NoCashSessionOpen);
            }
            else
            {
                return Result<CreatePurchaseResponse>.Failure(CreatePurchaseErrors.NoAssignedCashDrawer);
            }
        }

        foreach (var paymentRequest in command.Payments)
        {
            var method = (PurchasePaymentMethod)paymentRequest.Method;

            var purchasePayment = PurchasePayment.Create(
                method,
                paymentRequest.Amount,
                paymentRequest.Date,
                paymentRequest.Reference,
                paymentRequest.Notes);

            purchase.AddPayment(purchasePayment);
            openSession!.RegisterPurchaseExpense(paymentRequest.Amount, purchase.Id, userId, method);
        }

        await _purchaseRepository.AddAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreatePurchaseResponse>.Success(new CreatePurchaseResponse(
            purchase.Id,
            purchase.SupplierId,
            supplierName,
            purchase.InvoiceNumber,
            purchase.Notes,
            purchase.IvaPct,
            purchase.IngresosBrutosPct,
            (int)purchase.Status,
            purchase.Status.ToString(),
            purchase.TotalAmount,
            purchase.TaxAmount,
            purchase.GrandTotal,
            purchase.TotalPaid,
            purchase.PendingAmount,
            purchase.CreatedAt,
            purchase.Details.Select(d => new CreatePurchaseDetailResponse(
                d.ProductId,
                d.ProductName,
                d.Quantity,
                d.UnitCost,
                d.TotalAmount)).ToList(),
            purchase.Payments.Select(p => new CreatePurchasePaymentResponse(
                p.Id,
                (int)p.Method,
                p.Method.ToString(),
                p.Amount,
                p.Reference,
                p.Notes,
                p.Date)).ToList()));
    }
}
