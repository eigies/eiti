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
    private readonly IChequeRepository _chequeRepository;
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
        IChequeRepository chequeRepository,
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
        _chequeRepository = chequeRepository;
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

        // "Anular los pagos": se revierten los cobros vinculados PRIMERO (vuelven la venta a pendiente y el
        // stock a reservado), y luego se cancela la venta como pendiente. Así el stock no se maneja dos veces.
        if (hasCcPayments && command.RefundMode == CcCancellationRefundMode.ReversePayments)
        {
            var reversal = await ReverseLinkedCobrosAsync(sale, companyId, _currentUserService.UserId!, cancellationToken);
            if (reversal.IsFailure)
                return reversal;
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

        // "Saldo a favor": el cliente recupera lo cobrado como crédito (no toca caja). El stock ya se devolvió
        // arriba según el estado de la venta. ("Anular los pagos" ya se resolvió revirtiendo los cobros antes).
        if (hasCcPayments && command.RefundMode == CcCancellationRefundMode.Credit)
        {
            if (sale.CustomerId is null)
                return Result.Failure(CancelSaleErrors.CustomerNotFound);

            var customer = await _customerRepository.GetByIdAsync(sale.CustomerId, companyId, cancellationToken);
            if (customer is null)
                return Result.Failure(CancelSaleErrors.CustomerNotFound);

            customer.AddCredit(ccTotal);
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
    private async Task<Result> ReverseLinkedCobrosAsync(Sale sale, CompanyId companyId, UserId userId, CancellationToken cancellationToken)
    {
        var paymentIds = sale.CcPayments
            .Where(p => p.Status == SaleCcPaymentStatus.Active && p.CustomerPaymentId.HasValue)
            .Select(p => p.CustomerPaymentId!.Value)
            .Distinct()
            .ToList();

        foreach (var paymentId in paymentIds)
        {
            var payment = await _customerPaymentRepository.GetByIdAsync(paymentId, companyId.Value, cancellationToken);
            if (payment is null || payment.Status == SaleCcPaymentStatus.Cancelled)
                continue;

            var customer = await _customerRepository.GetByIdAsync(new CustomerId(payment.CustomerId), companyId, cancellationToken);
            if (customer is null)
                return Result.Failure(CancelSaleErrors.CustomerNotFound);

            CashSession? session;
            if (payment.Method == SalePaymentMethod.Cash)
            {
                var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
                    _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
                if (resolve.Status != CashSessionResolveStatus.Resolved)
                    return Result.Failure(CancelSaleErrors.NoOpenSessionForRefund);
                session = resolve.Session;
            }
            else
            {
                session = await CashSessionResolver.ResolveOpenSessionBestEffortAsync(
                    _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
            }

            await CustomerPaymentReversal.ReverseAsync(
                payment,
                customer,
                session,
                _saleRepository,
                _branchProductStockRepository,
                _stockMovementRepository,
                _customerRepository,
                _chequeRepository,
                companyId,
                userId,
                cancellationToken);
        }

        return Result.Success();
    }
}
