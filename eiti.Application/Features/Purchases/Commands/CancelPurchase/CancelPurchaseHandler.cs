using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Purchases.Common;
using eiti.Application.Features.Suppliers.Common;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Common;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Purchases;
using eiti.Domain.Stock;
using eiti.Domain.Suppliers;
using eiti.Domain.Users;
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
    private readonly ISupplierPaymentOrderService _supplierPaymentOrderService;
    private readonly ISupplierCreditNoteRepository _creditNoteRepository;
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
        ISupplierPaymentOrderService supplierPaymentOrderService,
        ISupplierCreditNoteRepository creditNoteRepository,
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
        _supplierPaymentOrderService = supplierPaymentOrderService;
        _creditNoteRepository = creditNoteRepository;
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

        // Una NC vigente sobre la compra no puede sobrevivirla: su crédito quedaría a favor por una compra que ya no
        // existe. Espejo de CancelSale y de CreateSupplierCreditNote (no emite NC sobre una compra anulada). Se
        // anulan juntas solo si el usuario lo confirmó; sin eso, el error las nombra para que el front pregunte.
        var linkedNotes = (await _creditNoteRepository.ListByPurchaseAsync(companyId.Value, purchase.Id, cancellationToken))
            .Where(note => note.Status == CreditNoteStatus.Active)
            .ToList();
        if (linkedNotes.Count > 0 && !command.CancelLinkedCreditNotes)
            return Result.Failure(CancelPurchaseErrors.HasActiveCreditNote(
                linkedNotes.Select(note => (note.Code, note.Amount)).ToList()));

        // Pagos imputados a esta compra, sin las imputaciones de esas NC: a esas las deshace la anulación de cada
        // nota. Contarlas también como "lo pagado" devolvería dos veces la misma plata al saldo.
        var linkedNoteIds = linkedNotes.Select(note => note.Id).ToHashSet();
        var activeAllocations = purchase.Payments
            .Where(p => p.Status == PurchasePaymentStatus.Active
                && !(p.CreditNoteId is { } noteId && linkedNoteIds.Contains(noteId)))
            .ToList();
        var paidTotal = activeAllocations.Sum(p => p.Amount);
        var hasPayments = paidTotal > 0;

        if (hasPayments && command.RefundMode is null)
            return Result.Failure(CancelPurchaseErrors.RefundModeRequired);

        var reversePayments = hasPayments && command.RefundMode == PurchaseCancellationRefundMode.ReversePayments;

        // ── Todo lo que puede fallar se resuelve antes de tocar nada ──

        Supplier? supplier = null;
        if (hasPayments || linkedNotes.Count > 0)
        {
            supplier = await _supplierRepository.GetByIdAsync(purchase.SupplierId, companyId.Value, cancellationToken);
            if (supplier is null)
                return Result.Failure(CancelPurchaseErrors.SupplierNotFound);
        }

        FundingReversal? fundingReversal = null;
        if (reversePayments)
        {
            var prepared = await PrepareFundingReversalAsync(activeAllocations, companyId, userId, cancellationToken);
            if (prepared.IsFailure)
                return Result.Failure(prepared.Error);
            fundingReversal = prepared.Value;
        }

        var noteCancellations = new List<SupplierCreditNoteCancellation>();
        CashSession? noteSession = null;
        if (linkedNotes.Count > 0)
        {
            // Anular una NC registra su movimiento neutro en la caja, igual que anularla sola.
            var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
                _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
            if (resolve.Status != CashSessionResolveStatus.Resolved)
                return Result.Failure(resolve.Status == CashSessionResolveStatus.NoAssignedDrawer
                    ? CancelPurchaseErrors.CreditNoteNoAssignedCashDrawer
                    : CancelPurchaseErrors.CreditNoteNoCashSessionOpen);
            noteSession = resolve.Session!;

            foreach (var note in linkedNotes)
            {
                noteCancellations.Add(await SupplierCreditNoteCancellation.PrepareAsync(
                    note, _purchaseRepository, companyId.Value, cancellationToken));
            }

            // El saldo a favor no puede quedar negativo: lo que sacan las notas (su importe menos lo que vuelve al
            // desimputarlas) tiene que alcanzar con el saldo actual más lo que devuelve la compra en el modo elegido.
            var creditFromPurchase = !hasPayments
                ? 0m
                : reversePayments
                    ? await fundingReversal!.CreditDeltaAsync(_purchaseRepository, companyId, cancellationToken)
                    : paidTotal;
            var balanceAfter = supplier!.CreditBalance + noteCancellations.Sum(c => c.CreditDelta) + creditFromPurchase;
            if (balanceAfter < 0m)
                return Result.Failure(CancelPurchaseErrors.InsufficientCreditForCreditNotes(
                    linkedNotes.Select(note => note.Code).ToList(), -balanceAfter));
        }

        // ── Mutaciones ──

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

        // Las NC primero: deshacen sus imputaciones (en esta compra y en otras, que vuelven a pendiente).
        foreach (var cancellation in noteCancellations)
        {
            cancellation.Apply(supplier!, noteSession!, userId);
        }

        purchase.Cancel();

        if (fundingReversal is not null)
        {
            await ApplyFundingReversalAsync(purchase, activeAllocations, fundingReversal, supplier!, companyId, userId, cancellationToken);
        }
        else if (hasPayments) // Credit: lo pagado queda como saldo a favor.
        {
            foreach (var allocation in activeAllocations)
                purchase.CancelPayment(allocation.Id);

            supplier!.AddCredit(paidTotal);
        }

        if (supplier is not null)
        {
            // El saldo a favor que queda se imputa FIFO a las compras pendientes, también a las que reabrió la
            // anulación de una NC: así no queda crédito suelto conviviendo con deuda.
            if ((hasPayments && !reversePayments) || noteCancellations.Count > 0)
            {
                await SupplierCreditApplicator.ApplyToPendingPurchasesAsync(
                    supplier, companyId.Value, _purchaseRepository, excludePurchaseId: purchase.Id, cancellationToken);
            }

            _supplierRepository.Update(supplier);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // "Revertir el pago", preparado: los pagos de proveedor que financiaron la compra y la caja para revertirlos.
    private sealed record FundingReversal(IReadOnlyList<SupplierPayment> Payments, decimal UnlinkedCredit, CashSession? Session)
    {
        // Cuánto cambia el saldo a favor al aplicarla: lo que cambia cada pago revertido (vuelve lo que imputó en
        // todas sus compras y sale su importe) más el saldo a favor consumido sin un pago puntual detrás.
        public async Task<decimal> CreditDeltaAsync(
            IPurchaseRepository purchaseRepository, CompanyId companyId, CancellationToken cancellationToken)
        {
            var delta = UnlinkedCredit;
            foreach (var payment in Payments)
                delta += await SupplierPaymentReversal.CreditDeltaAsync(payment, purchaseRepository, companyId, cancellationToken);
            return delta;
        }
    }

    // Revierte por completo cada pago de proveedor que financió esta compra (reintegra caja en efectivo, devuelve
    // cheque a cartera). Un pago que además financiaba otras compras las devuelve a Pendiente. Acá solo se lee y se
    // valida: si algo impide revertir, la anulación se rechaza antes de tocar nada.
    private async Task<Result<FundingReversal>> PrepareFundingReversalAsync(
        IReadOnlyList<PurchasePayment> activeAllocations,
        CompanyId companyId,
        UserId userId,
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
                return Result.Failure<FundingReversal>(CancelPurchaseErrors.SupplierPaymentNotFound);
            if (payment.Status != PurchasePaymentStatus.Cancelled)
                payments.Add(payment);
        }

        // Un pago que comparte orden con otros medios no se revierte solo: hay que anular la orden.
        var reversible = await _supplierPaymentOrderService.EnsureReversibleAloneAsync(payments, companyId, cancellationToken);
        if (reversible.IsFailure)
            return Result.Failure<FundingReversal>(reversible.Error);

        // Efectivo requiere caja abierta para reintegrar; otros métodos van best-effort.
        CashSession? session;
        if (payments.Any(p => p.Method == PurchasePaymentMethod.Cash))
        {
            var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
                _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
            if (resolve.Status == CashSessionResolveStatus.NoAssignedDrawer)
                return Result.Failure<FundingReversal>(CancelPurchaseErrors.NoAssignedCashDrawer);
            if (resolve.Status == CashSessionResolveStatus.NoSessionOpen)
                return Result.Failure<FundingReversal>(CancelPurchaseErrors.NoCashSessionOpen);
            session = resolve.Session;
        }
        else
        {
            session = await CashSessionResolver.ResolveOpenSessionBestEffortAsync(
                _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
        }

        var unlinkedCredit = activeAllocations.Where(a => !a.SupplierPaymentId.HasValue).Sum(a => a.Amount);
        return Result.Success(new FundingReversal(payments, unlinkedCredit, session));
    }

    private async Task ApplyFundingReversalAsync(
        Purchase purchase,
        IReadOnlyList<PurchasePayment> activeAllocations,
        FundingReversal reversal,
        Supplier supplier,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        foreach (var payment in reversal.Payments)
        {
            await _supplierPaymentOrderService.ReversePaymentAsync(
                payment, supplier, reversal.Session, companyId, userId, cancellationToken);
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
    }
}
