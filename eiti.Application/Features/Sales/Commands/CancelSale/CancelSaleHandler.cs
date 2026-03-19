using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
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

            var openSession = await _cashSessionRepository.GetAnyOpenByBranchAsync(
                sale.BranchId,
                companyId,
                cancellationToken);

            if (openSession is not null)
            {
                openSession.RegisterSaleCancel(sale.TotalAmount, sale.Id.Value, _currentUserService.UserId!);
            }

            sale.Cancel();
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

            var openSessionForPaid = await _cashSessionRepository.GetAnyOpenByBranchAsync(
                sale.BranchId,
                companyId,
                cancellationToken);

            if (openSessionForPaid is not null)
            {
                openSessionForPaid.RegisterSaleCancel(sale.TotalAmount, sale.Id.Value, _currentUserService.UserId!);
            }

            sale.Cancel();
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
}
