using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.AddCcPayment;

public sealed class AddCcPaymentHandler : IRequestHandler<AddCcPaymentCommand, Result<AddCcPaymentResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCcPaymentHandler(
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

    public async Task<Result<AddCcPaymentResponse>> Handle(AddCcPaymentCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<AddCcPaymentResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;
        if (companyId is null)
        {
            return Result<AddCcPaymentResponse>.Failure(AddCcPaymentErrors.Unauthorized);
        }

        if (!Enum.IsDefined(typeof(SalePaymentMethod), request.IdPaymentMethod))
        {
            return Result<AddCcPaymentResponse>.Failure(AddCcPaymentErrors.InvalidPaymentMethod);
        }

        var sale = await _saleRepository.GetByIdWithCcPaymentsAsync(new SaleId(request.SaleId), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result<AddCcPaymentResponse>.Failure(AddCcPaymentErrors.SaleNotFound);
        }

        if (!sale.IsCuentaCorriente)
        {
            return Result<AddCcPaymentResponse>.Failure(AddCcPaymentErrors.NotCuentaCorriente);
        }

        var wasPaid = sale.SaleStatus == SaleStatus.Paid;
        SaleCcPayment payment;

        try
        {
            payment = sale.AddCcPayment(
                (SalePaymentMethod)request.IdPaymentMethod,
                request.Amount,
                request.Date,
                request.Notes);
        }
        catch (InvalidOperationException ex)
        {
            return Result<AddCcPaymentResponse>.Failure(
                Error.Validation("Sales.AddCcPayment.InvalidInput", ex.Message));
        }

        if (!wasPaid && sale.SaleStatus == SaleStatus.Paid)
        {
            foreach (var detail in sale.Details)
            {
                var stock = await _branchProductStockRepository.GetOrCreateAsync(
                    sale.BranchId,
                    detail.ProductId,
                    companyId,
                    cancellationToken);

                stock.ConfirmSaleOut(detail.Quantity);
                await _stockMovementRepository.AddAsync(
                    StockMovement.Create(
                        companyId,
                        sale.BranchId,
                        stock.ProductId,
                        stock.Id,
                        StockMovementType.SaleOut,
                        detail.Quantity,
                        "Sale",
                        sale.Id.Value,
                        "Stock confirmed as sold (CC fully paid).",
                        _currentUserService.UserId),
                    cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AddCcPaymentResponse>.Success(
            new AddCcPaymentResponse(
                payment.Id.Value,
                payment.SaleId.Value,
                (int)payment.Method,
                payment.Method.ToString(),
                payment.Amount,
                payment.Date,
                payment.Notes,
                (int)payment.Status,
                payment.Status.ToString(),
                payment.CreatedAt,
                payment.CancelledAt));
    }
}
