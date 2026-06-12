using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Cash;
using eiti.Domain.Products;
using eiti.Domain.Sales;
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
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISaleTransportAssignmentRepository _saleTransportAssignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelSaleHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ICashSessionRepository cashSessionRepository,
        ISaleTransportAssignmentRepository saleTransportAssignmentRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _cashSessionRepository = cashSessionRepository;
        _saleTransportAssignmentRepository = saleTransportAssignmentRepository;
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

            var ccCancelledLinesOnHold = CaptureActiveCcLines(sale);
            sale.Cancel();
            CancelActiveCcPayments(sale);
            await RegisterCcCancellationIfNeeded(sale, ccCancelledLinesOnHold, cancellationToken);
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

            var ccCancelledLinesOnPaid = CaptureActiveCcLines(sale);
            sale.Cancel();
            CancelActiveCcPayments(sale);
            await RegisterCcCancellationIfNeeded(sale, ccCancelledLinesOnPaid, cancellationToken);
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

    private static List<(SalePaymentMethod Method, decimal Amount)> CaptureActiveCcLines(Sale sale)
    {
        return sale.CcPayments
            .Where(p => p.Status == SaleCcPaymentStatus.Active)
            .GroupBy(p => p.Method)
            .Select(g => (g.Key, g.Sum(p => p.Amount)))
            .ToList();
    }

    private async Task RegisterCcCancellationIfNeeded(
        Sale sale,
        IReadOnlyList<(SalePaymentMethod Method, decimal Amount)> cancelledLines,
        CancellationToken cancellationToken)
    {
        if (!sale.IsCuentaCorriente || cancelledLines.All(line => line.Amount <= 0) || _currentUserService.UserId is null)
            return;

        var session = await _cashSessionRepository.GetAnyOpenByBranchAsync(
            sale.BranchId,
            sale.CompanyId,
            cancellationToken);

        if (session is not null)
            session.RegisterCcPaymentCancellation(cancelledLines, sale.Id.Value, _currentUserService.UserId);
    }
}
