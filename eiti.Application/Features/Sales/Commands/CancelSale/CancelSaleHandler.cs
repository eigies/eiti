using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Customers.Common;
using eiti.Application.Features.Sales.Common;
using eiti.Domain.Cash;
using eiti.Domain.Common;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Users;
using eiti.Domain.Stock;
using eiti.Domain.Transport;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CancelSale;

public sealed class CancelSaleHandler : IRequestHandler<CancelSaleCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISaleTransportAssignmentRepository _saleTransportAssignmentRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerPaymentRepository _customerPaymentRepository;
    private readonly ICustomerReceiptService _customerReceiptService;
    private readonly ISaleInvoicingService _saleInvoicingService;
    private readonly ICustomerCreditNoteRepository _creditNoteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelSaleHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ISaleTransportAssignmentRepository saleTransportAssignmentRepository,
        ICustomerRepository customerRepository,
        ICustomerPaymentRepository customerPaymentRepository,
        ICustomerReceiptService customerReceiptService,
        ISaleInvoicingService saleInvoicingService,
        ICustomerCreditNoteRepository creditNoteRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _saleTransportAssignmentRepository = saleTransportAssignmentRepository;
        _customerRepository = customerRepository;
        _customerPaymentRepository = customerPaymentRepository;
        _customerReceiptService = customerReceiptService;
        _saleInvoicingService = saleInvoicingService;
        _creditNoteRepository = creditNoteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelSaleCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var sale = await _saleRepository.GetByIdAsync(new SaleId(command.Id), cancellationToken);

        if (sale is null || sale.CompanyId != companyId)
        {
            return Result.Failure(CancelSaleErrors.NotFound);
        }

        if (sale.SaleStatus == SaleStatus.Cancel)
        {
            return Result.Failure(CancelSaleErrors.AlreadyCancelled);
        }

        // Una NC vigente sobre la venta tiene que anularse primero: si no, lo que imputó vuelve como saldo a favor
        // y el crédito sobrevive a la venta que lo justificaba (NCC-003 de LACTEOS BARRAZA, MAT-3844). Mismo
        // invariante que CreateCustomerCreditNote, que no emite NC sobre una venta anulada. Va antes de pedir
        // RefundMode: no tiene sentido elegir qué hacer con lo cobrado de una anulación que se va a rechazar.
        var activeCreditNotes = (await _creditNoteRepository.ListBySaleAsync(companyId.Value, sale.Id.Value, cancellationToken))
            .Where(note => note.Status == CreditNoteStatus.Active)
            .Select(note => note.Code)
            .ToList();
        if (activeCreditNotes.Count > 0)
        {
            return Result.Failure(CancelSaleErrors.HasActiveCreditNote(activeCreditNotes));
        }

        var existingTransportAssignmentId = sale.TransportAssignmentId;

        // Cobros de cuenta corriente imputados a esta venta (antes de cancelarlos).
        var ccCancelledLines = CaptureActiveCcLines(sale);
        var ccTotal = ccCancelledLines.Sum(line => line.Amount);
        var hasCcPayments = sale.IsCuentaCorriente && ccTotal > 0;

        // Con cobros imputados hay que decidir qué hacer con esa plata.
        if (hasCcPayments && command.RefundMode is null)
        {
            return Result.Failure(CancelSaleErrors.RefundModeRequired);
        }

        // Saldo a favor que vuelve al cliente al anular. "Saldo a favor": todo lo cobrado.
        var creditToReturn = hasCcPayments && command.RefundMode == CcCancellationRefundMode.Credit ? ccTotal : 0m;

        // La NC sale al receptor de la factura, no al de la ficha: si la ficha cambió después de facturar, el
        // usuario tiene que verlo antes de emitir un comprobante fiscal a un receptor que no es el de la ficha.
        // Si el servicio fiscal no responde, se anula igual (anular es una decisión del negocio) y la NC no
        // sale hasta poder verificarlo: queda para reintentar desde el detalle de la venta.
        if (_saleInvoicingService.IsEnabled && !command.ConfirmInvoiceReceiver)
        {
            var receivers = await _saleInvoicingService.GetFiscalReceiversAsync(sale, cancellationToken);
            if (receivers.IsAvailable && receivers.InvoiceIsLive && !receivers.CustomerMatchesInvoice)
            {
                return Result.Failure(CancelSaleErrors.InvoiceReceiverChanged);
            }
        }

        // "Anular los pagos": se revierten los cobros vinculados PRIMERO (vuelven la venta a pendiente y el
        // stock a reservado), y luego se cancela la venta como pendiente. Así el stock no se maneja dos veces.
        if (hasCcPayments && command.RefundMode == CcCancellationRefundMode.ReversePayments)
        {
            var reversal = await ReverseLinkedCobrosAsync(sale, companyId, _currentUserService.UserId!, cancellationToken);
            if (reversal.IsFailure)
                return Result.Failure(reversal.Error);

            // Imputaciones de saldo a favor sin un cobro que revertir: el aplicado al crear la venta
            // (CreateCcSaleHandler, CustomerPaymentId nulo) o el FIFO de una NC / de otra anulación en modo
            // "Saldo a favor" (CustomerPaymentId vacío). No hay caja que reintegrar, pero sí saldo que restituir:
            // sin esto, CancelActiveCcPayments las anula y el saldo se pierde. Mismo criterio que CancelPurchaseHandler.
            creditToReturn = sale.CcPayments
                .Where(p => p.Status == SaleCcPaymentStatus.Active
                    && p.Method == SalePaymentMethod.CustomerCredit
                    && !(p.CustomerPaymentId is { } paymentId && reversal.Value.Contains(paymentId)))
                .Sum(p => p.Amount);
        }

        if (sale.SaleStatus == SaleStatus.OnHold)
        {
            var currentDetails = sale.Details
                .GroupBy(detail => detail.ProductId.Value)
                .Select(group => new
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(item => item.Quantity),
                    UnitPrice = group.First().UnitPrice
                })
                .ToList();

            foreach (var detail in currentDetails)
            {
                var stock = await _branchProductStockRepository.GetOrCreateAsync(
                    sale.BranchId,
                    new ProductId(detail.ProductId),
                    companyId,
                    cancellationToken);

                try
                {
                    stock.ReleaseReservation(detail.Quantity);
                }
                catch (ArgumentException ex)
                {
                    return Result.Failure(Error.Validation("Sales.Cancel.InvalidQuantity", ex.Message));
                }
                catch (InvalidOperationException ex)
                {
                    return Result.Failure(Error.Conflict("Sales.Cancel.InvalidReservation", ex.Message));
                }

                await _stockMovementRepository.AddAsync(
                    StockMovement.Create(
                        companyId,
                        sale.BranchId,
                        stock.ProductId,
                        stock.Id,
                        StockMovementType.ReleaseReservation,
                        detail.Quantity,
                        "Sale",
                        sale.Id.Value,
                        "Stock reservation released due to sale cancellation.",
                        _currentUserService.UserId),
                    cancellationToken);
            }
        }
        else if (sale.SaleStatus == SaleStatus.Paid)
        {
            var currentDetails = sale.Details
                .GroupBy(detail => detail.ProductId.Value)
                .Select(group => new
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(item => item.Quantity),
                    UnitPrice = group.First().UnitPrice
                })
                .ToList();

            foreach (var detail in currentDetails)
            {
                var stock = await _branchProductStockRepository.GetOrCreateAsync(
                    sale.BranchId,
                    new ProductId(detail.ProductId),
                    companyId,
                    cancellationToken);

                try
                {
                    stock.ApplyManualEntry(detail.Quantity);
                }
                catch (ArgumentException ex)
                {
                    return Result.Failure(Error.Validation("Sales.Cancel.InvalidQuantity", ex.Message));
                }

                await _stockMovementRepository.AddAsync(
                    StockMovement.Create(
                        companyId,
                        sale.BranchId,
                        stock.ProductId,
                        stock.Id,
                        StockMovementType.SaleReturn,
                        detail.Quantity,
                        "Sale",
                        sale.Id.Value,
                        "Stock returned due to sale cancellation.",
                        _currentUserService.UserId),
                    cancellationToken);
            }

            CashSession? openSessionForPaid = null;
            Guid? originalCashSessionId = null;

            if (sale.CashSessionId is not null)
            {
                var originalSession = await _cashSessionRepository.GetByIdAsync(
                    sale.CashSessionId,
                    companyId,
                    cancellationToken);

                if (originalSession is null)
                {
                    return Result.Failure(CancelSaleErrors.CashSessionNotFound);
                }

                if (originalSession.Status != CashSessionStatus.Open)
                {
                    if (!_currentUserService.HasPermission(PermissionCodes.SalesCancelHistorical))
                        return Result.Failure(CancelSaleErrors.CashSessionClosed);

                    var currentOpenSession = await _cashSessionRepository.GetOpenForBranchAsync(
                        originalSession.BranchId,
                        originalSession.CashDrawerId,
                        companyId,
                        cancellationToken);

                    if (currentOpenSession is null)
                        return Result.Failure(CancelSaleErrors.NoOpenSessionForHistoricalCancel);

                    originalCashSessionId = originalSession.Id.Value;
                    openSessionForPaid = currentOpenSession;
                }
                else
                {
                    openSessionForPaid = originalSession;
                }
            }
            else if (sale.CashDrawerId is not null)
            {
                openSessionForPaid = await _cashSessionRepository.GetOpenForBranchAsync(
                    sale.BranchId,
                    sale.CashDrawerId,
                    companyId,
                    cancellationToken);

                if (openSessionForPaid is null)
                {
                    return Result.Failure(CancelSaleErrors.CashSessionNotFound);
                }
            }

            if (openSessionForPaid is not null)
            {
                openSessionForPaid.RegisterSaleCancellation(sale.Payments, sale.Id.Value, _currentUserService.UserId!, originalCashSessionId);
            }
        }

        sale.Cancel();
        CancelActiveCcPayments(sale);

        // El cliente recupera el saldo a favor (no toca caja): en "Saldo a favor", lo cobrado; en "Anular los
        // pagos", el saldo a favor consumido que no volvió con la reversa de un cobro. El stock ya se devolvió
        // arriba según el estado de la venta.
        if (creditToReturn > 0)
        {
            if (sale.CustomerId is null)
                return Result.Failure(CancelSaleErrors.CustomerNotFound);

            var customer = await _customerRepository.GetByIdAsync(sale.CustomerId, companyId, cancellationToken);
            if (customer is null)
                return Result.Failure(CancelSaleErrors.CustomerNotFound);

            customer.AddCredit(creditToReturn);

            // Aplicar el saldo a favor liberado a las ventas CC pendientes (FIFO), igual que un cobro:
            // así no queda crédito "suelto" conviviendo con deuda pendiente.
            var application = await CustomerCreditApplicator.ApplyToPendingCcSalesAsync(
                customer, companyId, _saleRepository, cancellationToken);
            _customerRepository.Update(customer);

            // Confirmar stock (reservado -> vendido) de las ventas que pasaron a pagadas con ese crédito.
            foreach (var paidSale in application.SalesNowPaidEntities)
            {
                foreach (var detail in paidSale.Details)
                {
                    var paidStock = await _branchProductStockRepository.GetOrCreateAsync(
                        paidSale.BranchId, detail.ProductId, companyId, cancellationToken);

                    paidStock.ConfirmSaleOut(detail.Quantity);
                    await _stockMovementRepository.AddAsync(
                        StockMovement.Create(
                            companyId,
                            paidSale.BranchId,
                            paidStock.ProductId,
                            paidStock.Id,
                            StockMovementType.SaleOut,
                            detail.Quantity,
                            "Sale",
                            paidSale.Id.Value,
                            "Stock confirmado vendido (CC pagada con saldo a favor liberado al anular).",
                            _currentUserService.UserId),
                        cancellationToken);
                }
            }
        }

        if (existingTransportAssignmentId is not null)
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

        // Una venta ya facturada se anula ante el fisco con una nota de crédito electrónica.
        // Deliberadamente NO se propaga el error: si el servicio fiscal está caído, la anulación
        // se completa igual y la NC queda Rechazada para reintentarla desde el detalle. Anular es
        // una decisión del negocio; no puede quedar rehén de la disponibilidad de un tercero.
        if (_saleInvoicingService.IsEnabled)
        {
            // Si la venta no tiene factura autorizada, esto no hace nada.
            await _saleInvoicingService.IssueCreditNoteAsync(sale, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static void CancelActiveCcPayments(Sale sale)
    {
        foreach (var payment in sale.CcPayments.Where(p => p.Status == SaleCcPaymentStatus.Active))
        {
            payment.Cancel();
        }
    }

    private static List<(SalePaymentMethod Method, decimal Amount, Guid? SaleCcPaymentId)> CaptureActiveCcLines(Sale sale)
    {
        return sale.CcPayments
            .Where(p => p.Status == SaleCcPaymentStatus.Active)
            .GroupBy(p => p.Method)
            .Select(g => (g.Key, g.Sum(p => p.Amount), g.Count() == 1 ? g.Single().Id.Value : (Guid?)null))
            .ToList();
    }

    // "Anular los pagos": revierte cada cobro vinculado a la venta (efectivo a caja, crédito y cheque),
    // reusando la misma lógica que "anular cobro". Requiere caja abierta para cobros en efectivo.
    // Devuelve los Id de los cobros revertidos.
    private async Task<Result<HashSet<Guid>>> ReverseLinkedCobrosAsync(Sale sale, CompanyId companyId, UserId userId, CancellationToken cancellationToken)
    {
        var paymentIds = sale.CcPayments
            .Where(p => p.Status == SaleCcPaymentStatus.Active && p.CustomerPaymentId.HasValue)
            .Select(p => p.CustomerPaymentId!.Value)
            .Distinct()
            .ToList();

        var payments = new List<CustomerPayment>();
        foreach (var paymentId in paymentIds)
        {
            var payment = await _customerPaymentRepository.GetByIdAsync(paymentId, companyId.Value, cancellationToken);
            if (payment is not null && payment.Status != SaleCcPaymentStatus.Cancelled)
                payments.Add(payment);
        }

        // Un cobro que comparte recibo con otros medios no se revierte solo: hay que anular el recibo.
        var reversible = await _customerReceiptService.EnsureReversibleAloneAsync(payments, companyId, cancellationToken);
        if (reversible.IsFailure)
            return Result.Failure<HashSet<Guid>>(reversible.Error);

        foreach (var payment in payments)
        {
            var customer = await _customerRepository.GetByIdAsync(new CustomerId(payment.CustomerId), companyId, cancellationToken);
            if (customer is null)
                return Result.Failure<HashSet<Guid>>(CancelSaleErrors.CustomerNotFound);

            CashSession? session;
            if (payment.Method == SalePaymentMethod.Cash)
            {
                var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
                    _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
                if (resolve.Status != CashSessionResolveStatus.Resolved)
                    return Result.Failure<HashSet<Guid>>(CancelSaleErrors.NoOpenSessionForRefund);
                session = resolve.Session;
            }
            else
            {
                session = await CashSessionResolver.ResolveOpenSessionBestEffortAsync(
                    _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
            }

            await _customerReceiptService.ReversePaymentAsync(
                payment, customer, session, companyId, userId, cancellationToken);
        }

        return Result.Success(payments.Select(p => p.Id).ToHashSet());
    }
}
