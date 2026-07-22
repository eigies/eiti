using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Purchases.Common;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Products;
using eiti.Domain.Purchases;
using eiti.Domain.Stock;
using eiti.Domain.Suppliers;
using MediatR;

namespace eiti.Application.Features.Purchases.Commands.CancelPurchase;

public sealed class CancelPurchaseHandler : IRequestHandler<CancelPurchaseCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IChequeRepository _chequeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPurchaseHandler(
        ICurrentUserService currentUserService,
        IPurchaseRepository purchaseRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ISupplierRepository supplierRepository,
        ISupplierPaymentRepository supplierPaymentRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IChequeRepository chequeRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _purchaseRepository = purchaseRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _supplierRepository = supplierRepository;
        _supplierPaymentRepository = supplierPaymentRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _chequeRepository = chequeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelPurchaseCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var purchase = await _purchaseRepository.GetByIdAsync(command.Id, companyId.Value, cancellationToken);
        if (purchase is null)
            return Result.Failure(CancelPurchaseErrors.NotFound);

        if (purchase.Status == PurchaseStatus.Cancelled)
            return Result.Failure(CancelPurchaseErrors.AlreadyCancelled);

        // Pagos imputados a esta compra (antes de tocar nada). Con pagos hay que decidir qué hacer con la plata.
        var activeAllocations = purchase.Payments
            .Where(p => p.Status == PurchasePaymentStatus.Active)
            .ToList();
        var paidTotal = activeAllocations.Sum(p => p.Amount);
        var hasPayments = paidTotal > 0;

        if (hasPayments && command.RefundMode is null)
            return Result.Failure(CancelPurchaseErrors.RefundModeRequired);

        // Revertir el stock que ingresó la compra.
        foreach (var detail in purchase.Details)
        {
            var stock = await _branchProductStockRepository.GetOrCreateAsync(
                new BranchId(purchase.BranchId),
                new ProductId(detail.ProductId),
                companyId,
                cancellationToken);

            try
            {
                stock.ApplyManualAdjustment(-(int)detail.Quantity);
            }
            catch (ArgumentException ex)
            {
                return Result.Failure(Error.Validation("Purchases.Cancel.InvalidQuantity", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(Error.Conflict("Purchases.Cancel.StockError", ex.Message));
            }

            await _stockMovementRepository.AddAsync(
                StockMovement.Create(
                    companyId,
                    new BranchId(purchase.BranchId),
                    stock.ProductId,
                    stock.Id,
                    StockMovementType.PurchaseReturn,
                    (int)detail.Quantity,
                    "Purchase",
                    purchase.Id,
                    "Stock reversed due to purchase cancellation.",
                    _currentUserService.UserId),
                cancellationToken);
        }

        purchase.Cancel();

        if (hasPayments)
        {
            var supplier = await _supplierRepository.GetByIdAsync(purchase.SupplierId, companyId.Value, cancellationToken);
            if (supplier is null)
                return Result.Failure(CancelPurchaseErrors.SupplierNotFound);

            if (command.RefundMode == PurchaseCancellationRefundMode.ReversePayments)
            {
                var reversal = await ReverseFundingPaymentsAsync(purchase, activeAllocations, supplier, companyId, userId, cancellationToken);
                if (reversal.IsFailure)
                    return reversal;
            }
            else // Credit: lo pagado queda como saldo a favor y se auto-aplica FIFO a compras pendientes.
            {
                foreach (var allocation in activeAllocations)
                    purchase.CancelPayment(allocation.Id);

                supplier.AddCredit(paidTotal);
                await SupplierCreditApplicator.ApplyToPendingPurchasesAsync(
                    supplier, companyId.Value, _purchaseRepository, excludePurchaseId: purchase.Id, cancellationToken);
                _supplierRepository.Update(supplier);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // "Revertir el pago": revierte por completo cada pago de proveedor que financió esta compra (reintegra caja
    // en efectivo, devuelve cheque a cartera). Un pago que además financiaba otras compras las devuelve a Pendiente.
    private async Task<Result> ReverseFundingPaymentsAsync(
        Purchase purchase,
        IReadOnlyList<PurchasePayment> activeAllocations,
        Supplier supplier,
        eiti.Domain.Companies.CompanyId companyId,
        eiti.Domain.Users.UserId userId,
        CancellationToken cancellationToken)
    {
        var paymentIds = activeAllocations
            .Where(a => a.SupplierPaymentId.HasValue)
            .Select(a => a.SupplierPaymentId!.Value)
            .Distinct()
            .ToList();

        var payments = new List<SupplierPayment>();
        foreach (var paymentId in paymentIds)
        {
            var payment = await _supplierPaymentRepository.GetByIdAsync(paymentId, companyId.Value, cancellationToken);
            if (payment is null)
                return Result.Failure(CancelPurchaseErrors.SupplierPaymentNotFound);
            if (payment.Status != PurchasePaymentStatus.Cancelled)
                payments.Add(payment);
        }

        // Efectivo requiere caja abierta para reintegrar; otros métodos van best-effort.
        CashSession? session;
        if (payments.Any(p => p.Method == PurchasePaymentMethod.Cash))
        {
            var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
                _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
            if (resolve.Status == CashSessionResolveStatus.NoAssignedDrawer)
                return Result.Failure(CancelPurchaseErrors.NoAssignedCashDrawer);
            if (resolve.Status == CashSessionResolveStatus.NoSessionOpen)
                return Result.Failure(CancelPurchaseErrors.NoCashSessionOpen);
            session = resolve.Session;
        }
        else
        {
            session = await CashSessionResolver.ResolveOpenSessionBestEffortAsync(
                _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
        }

        foreach (var payment in payments)
        {
            await SupplierPaymentReversal.ReverseAsync(
                payment, supplier, session, _purchaseRepository, _chequeRepository, companyId, userId, cancellationToken);
        }

        // Imputaciones sin pago de proveedor asociado (SupplierPaymentId null): son saldo a favor consumido
        // -no un pago puntual reversible en caja-, ya sea aplicado automáticamente al crear la compra
        // (CreatePurchaseHandler) o por el FIFO de otra cancelación en modo Credit. Al revertir la compra,
        // ese crédito vuelve al proveedor (no hay caja que reintegrar, pero sí saldo que restituir).
        foreach (var allocation in activeAllocations.Where(a => !a.SupplierPaymentId.HasValue && a.Status == PurchasePaymentStatus.Active))
        {
            supplier.AddCredit(allocation.Amount);
            purchase.CancelPayment(allocation.Id);
        }

        _supplierRepository.Update(supplier);
        return Result.Success();
    }
}
