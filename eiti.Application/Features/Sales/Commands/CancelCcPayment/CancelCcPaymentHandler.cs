using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CancelCcPayment;

public sealed class CancelCcPaymentHandler : IRequestHandler<CancelCcPaymentCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelCcPaymentHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelCcPaymentCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;
        if (companyId is null)
        {
            return Result.Failure(CancelCcPaymentErrors.Unauthorized);
        }

        var sale = await _saleRepository.GetByIdWithCcPaymentsAsync(new SaleId(request.SaleId), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result.Failure(CancelCcPaymentErrors.SaleNotFound);
        }

        if (!sale.IsCuentaCorriente)
        {
            return Result.Failure(CancelCcPaymentErrors.NotCuentaCorriente);
        }

        var wasPaid = sale.SaleStatus == SaleStatus.Paid;

        try
        {
            sale.CancelCcPayment(new SaleCcPaymentId(request.PaymentId));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(
                Error.Validation("Sales.CancelCcPayment.InvalidInput", ex.Message));
        }

        if (wasPaid && sale.SaleStatus == SaleStatus.OnHold)
        {
            foreach (var detail in sale.Details)
            {
                var stock = await _branchProductStockRepository.GetOrCreateAsync(
                    sale.BranchId,
                    detail.ProductId,
                    companyId,
                    cancellationToken);

                stock.RevertSaleOut(detail.Quantity);
                await _stockMovementRepository.AddAsync(
                    StockMovement.Create(
                        companyId,
                        sale.BranchId,
                        stock.ProductId,
                        stock.Id,
                        StockMovementType.Reserve,
                        detail.Quantity,
                        "Sale",
                        sale.Id.Value,
                        "Stock reverted to reserved (CC payment cancelled).",
                        _currentUserService.UserId),
                    cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
