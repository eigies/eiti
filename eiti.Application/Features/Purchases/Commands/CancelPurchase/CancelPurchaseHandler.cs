using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Branches;
using eiti.Domain.Products;
using eiti.Domain.Purchases;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Purchases.Commands.CancelPurchase;

public sealed class CancelPurchaseHandler : IRequestHandler<CancelPurchaseCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPurchaseHandler(
        ICurrentUserService currentUserService,
        IPurchaseRepository purchaseRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _purchaseRepository = purchaseRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelPurchaseCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var purchase = await _purchaseRepository.GetByIdAsync(command.Id, companyId.Value, cancellationToken);
        if (purchase is null)
            return Result.Failure(CancelPurchaseErrors.NotFound);

        if (purchase.Status == PurchaseStatus.Cancelled)
            return Result.Failure(CancelPurchaseErrors.AlreadyCancelled);

        if (purchase.Status == PurchaseStatus.Paid)
            return Result.Failure(CancelPurchaseErrors.CannotCancelPaid);

        // Reverse stock for each detail
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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
