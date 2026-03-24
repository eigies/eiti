using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Cash;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.AddCcPaymentGroup;

public sealed class AddCcPaymentGroupHandler : IRequestHandler<AddCcPaymentGroupCommand, Result<AddCcPaymentGroupResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCcPaymentGroupHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AddCcPaymentGroupResponse>> Handle(AddCcPaymentGroupCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<AddCcPaymentGroupResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;
        if (companyId is null)
        {
            return Result<AddCcPaymentGroupResponse>.Failure(AddCcPaymentGroupErrors.Unauthorized);
        }

        // Validate payment methods
        foreach (var method in request.Methods)
        {
            if (!Enum.IsDefined(typeof(SalePaymentMethod), method.IdPaymentMethod))
            {
                return Result<AddCcPaymentGroupResponse>.Failure(AddCcPaymentGroupErrors.InvalidPaymentMethod);
            }
        }

        var sale = await _saleRepository.GetByIdWithCcPaymentsAsync(new SaleId(request.SaleId), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result<AddCcPaymentGroupResponse>.Failure(AddCcPaymentGroupErrors.SaleNotFound);
        }

        if (!sale.IsCuentaCorriente)
        {
            return Result<AddCcPaymentGroupResponse>.Failure(AddCcPaymentGroupErrors.NotCuentaCorriente);
        }

        var wasPaid = sale.SaleStatus == SaleStatus.Paid;

        var methodLines = request.Methods
            .Select(m => ((SalePaymentMethod)m.IdPaymentMethod, m.Amount))
            .ToList();

        IReadOnlyList<SaleCcPayment> payments;
        try
        {
            payments = sale.AddCcPaymentGroup(methodLines, request.Date, request.Notes);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Result<AddCcPaymentGroupResponse>.Failure(
                Error.Validation("Sales.AddCcPaymentGroup.InvalidInput", ex.Message));
        }

        // Caja integration
        if (request.CashDrawerId.HasValue)
        {
            var session = await _cashSessionRepository.GetOpenForBranchAsync(
                sale.BranchId,
                new CashDrawerId(request.CashDrawerId.Value),
                companyId,
                cancellationToken);

            if (session is null)
            {
                return Result<AddCcPaymentGroupResponse>.Failure(AddCcPaymentGroupErrors.CashSessionRequired);
            }

            var totalAmount = payments.Sum(p => p.Amount);
            session.RegisterCcPaymentIncome(totalAmount, sale.Id.Value, _currentUserService.UserId!);
        }

        // Stock confirmation if sale transitioned to Paid
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

        var groupId = payments.First().GroupId!.Value;

        return Result<AddCcPaymentGroupResponse>.Success(
            new AddCcPaymentGroupResponse(
                groupId,
                payments.Select(p => new AddCcPaymentGroupItemResponse(
                    p.Id.Value,
                    p.SaleId.Value,
                    (int)p.Method,
                    p.Method.ToString(),
                    p.Amount,
                    p.Date,
                    p.Notes,
                    (int)p.Status,
                    p.Status.ToString(),
                    p.CreatedAt,
                    p.CancelledAt,
                    p.GroupId)).ToList()));
    }
}
