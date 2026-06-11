using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Customers;
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
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IBankRepository _bankRepository;
    private readonly IChequeRepository _chequeRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCcPaymentGroupHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IBankRepository bankRepository,
        IChequeRepository chequeRepository,
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _bankRepository = bankRepository;
        _chequeRepository = chequeRepository;
        _customerRepository = customerRepository;
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

        var effectiveCashDrawerId = await CashDrawerAccessPolicy.ResolveEffectiveDrawerIdAsync(
            _currentUserService,
            _cashDrawerRepository,
            request.CashDrawerId,
            cancellationToken);

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

        var remaining = sale.TotalAmount - sale.CcPaidTotal;
        var totalPayment = request.Methods.Sum(m => m.Amount);
        var excess = Math.Max(0m, totalPayment - remaining);
        var allowOverpayment = excess > 0;

        IReadOnlyList<SaleCcPayment> payments;
        try
        {
            payments = sale.AddCcPaymentGroup(methodLines, request.Date, request.Notes, allowOverpayment);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Result<AddCcPaymentGroupResponse>.Failure(
                Error.Validation("Sales.AddCcPaymentGroup.InvalidInput", ex.Message));
        }

        // Caja integration
        if (effectiveCashDrawerId.HasValue)
        {
            var session = await _cashSessionRepository.GetOpenForBranchAsync(
                sale.BranchId,
                new CashDrawerId(effectiveCashDrawerId.Value),
                companyId,
                cancellationToken);

            if (session is null)
            {
                return Result<AddCcPaymentGroupResponse>.Failure(AddCcPaymentGroupErrors.CashSessionRequired);
            }

            var ccGroupId = payments.First().GroupId!.Value;

            // Solo la porción en EFECTIVO de un pago de cuenta corriente entra al cajón físico.
            var cashAmount = payments.Where(p => p.Method == SalePaymentMethod.Cash).Sum(p => p.Amount);
            if (cashAmount > 0)
                session.RegisterCcPaymentIncome(cashAmount, sale.Id.Value, _currentUserService.UserId!, ccGroupId);

            // Transferencia y tarjeta de un cobro de cuenta corriente: se registran para que se VEAN en la
            // caja (conciliación), con dirección In pero SIN sumar al esperado de efectivo — exactamente
            // igual que en las ventas directas (ExpectedClosingAmount excluye TransferIncome/CardIncome).
            // Se usan los métodos CC (ReferenceType = CuentaCorriente + grupo) para que NO caigan bajo el
            // índice anti-duplicado IX_CashMovements_ReferenceId_Type (filtrado a ReferenceType = 'Sale'),
            // que es para ventas directas; una CC puede tener varios cobros del mismo tipo en el tiempo.
            var transferAmount = payments.Where(p => p.Method == SalePaymentMethod.Transfer).Sum(p => p.Amount);
            if (transferAmount > 0)
                session.RegisterCcTransferIncome(transferAmount, sale.Id.Value, _currentUserService.UserId!, ccGroupId);

            var cardAmount = payments.Where(p => p.Method == SalePaymentMethod.Card).Sum(p => p.Amount);
            if (cardAmount > 0)
                session.RegisterCcCardIncome(cardAmount, sale.Id.Value, _currentUserService.UserId!, ccGroupId);
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

        // Process card data and cheque data — match payment to its method line by method enum
        // (AddCcPaymentGroup skips zero-amount lines so we can't rely on positional index)
        var methodLineByMethod = request.Methods
            .GroupBy(m => (SalePaymentMethod)m.IdPaymentMethod)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var payment in payments)
        {
            if (!methodLineByMethod.TryGetValue(payment.Method, out var methodLine))
                continue;

            if (payment.Method == SalePaymentMethod.Card
                && methodLine.CardBankId.HasValue
                && methodLine.CardCuotas.HasValue)
            {
                var bank = await _bankRepository.GetByIdAsync(methodLine.CardBankId.Value, companyId!, cancellationToken);
                if (bank is not null)
                {
                    var plan = bank.InstallmentPlans.FirstOrDefault(p => p.Cuotas == methodLine.CardCuotas.Value && p.Active);
                    if (plan is not null)
                    {
                        var surchargeAmt = decimal.Round(payment.Amount * plan.SurchargePct / 100, 2, MidpointRounding.AwayFromZero);
                        payment.SetCardData(bank.Id, plan.Cuotas, plan.SurchargePct, surchargeAmt);
                    }
                }
            }

            if (payment.Method == SalePaymentMethod.Check && methodLine.Cheque is not null)
            {
                var chequeData = methodLine.Cheque;
                var cheque = Cheque.CreateForCcPayment(
                    companyId,
                    payment.Id.Value,
                    chequeData.BankId,
                    chequeData.Numero,
                    chequeData.Titular,
                    chequeData.CuitDni,
                    chequeData.Monto,
                    chequeData.FechaEmision,
                    chequeData.FechaVencimiento,
                    chequeData.Notas);
                await _chequeRepository.AddAsync(cheque, cancellationToken);
            }
        }

        Customer? creditCustomer = null;
        if (allowOverpayment && sale.CustomerId is not null)
        {
            creditCustomer = await _customerRepository.GetByIdAsync(sale.CustomerId, companyId, cancellationToken);
            if (creditCustomer is not null)
            {
                creditCustomer.AddCredit(excess);
                _customerRepository.Update(creditCustomer);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var groupId = payments.First().GroupId!.Value;

        return Result<AddCcPaymentGroupResponse>.Success(
            new AddCcPaymentGroupResponse(
                groupId,
                excess,
                creditCustomer?.CreditBalance ?? 0m,
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
                    p.GroupId,
                    p.CardBankId,
                    p.CardCuotas,
                    p.CardSurchargePct,
                    p.CardSurchargeAmt,
                    p.TotalCobrado)).ToList()));
    }
}
