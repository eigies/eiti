using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Customers.Common;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.AddCustomerPayment;

public sealed class AddCustomerPaymentHandler : IRequestHandler<AddCustomerPaymentCommand, Result<AddCustomerPaymentResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerPaymentRepository _customerPaymentRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IBankRepository _bankRepository;
    private readonly IChequeRepository _chequeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCustomerPaymentHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        ICustomerPaymentRepository customerPaymentRepository,
        ISaleRepository saleRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IBankRepository bankRepository,
        IChequeRepository chequeRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _customerPaymentRepository = customerPaymentRepository;
        _saleRepository = saleRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _bankRepository = bankRepository;
        _chequeRepository = chequeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AddCustomerPaymentResponse>> Handle(AddCustomerPaymentCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<AddCustomerPaymentResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        if (!Enum.IsDefined(typeof(SalePaymentMethod), command.Method)
            || command.Method == (int)SalePaymentMethod.CustomerCredit)
            return Result<AddCustomerPaymentResponse>.Failure(AddCustomerPaymentErrors.InvalidPaymentMethod);

        if (command.Amount <= 0)
            return Result<AddCustomerPaymentResponse>.Failure(AddCustomerPaymentErrors.InvalidAmount);

        var method = (SalePaymentMethod)command.Method;

        if (method == SalePaymentMethod.Check && command.Cheque is null)
            return Result<AddCustomerPaymentResponse>.Failure(AddCustomerPaymentErrors.ChequeRequired);

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(command.CustomerId), companyId, cancellationToken);
        if (customer is null)
            return Result<AddCustomerPaymentResponse>.Failure(AddCustomerPaymentErrors.CustomerNotFound);

        // Resolver caja abierta: exige caja propia (drawer asignado) o permiso CashDrawerViewAll.
        var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
            _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
        if (resolve.Status != CashSessionResolveStatus.Resolved)
            return Result<AddCustomerPaymentResponse>.Failure(resolve.Status == CashSessionResolveStatus.NoAssignedDrawer
                ? AddCustomerPaymentErrors.NoAssignedCashDrawer
                : AddCustomerPaymentErrors.NoCashSessionOpen);
        var session = resolve.Session!;

        if (BusinessDay.IsFromPreviousBusinessDay(session.OpenedAt))
            return Result<AddCustomerPaymentResponse>.Failure(AddCustomerPaymentErrors.CashSessionFromPreviousDay);

        var creditBefore = customer.CreditBalance;
        var amount = command.Amount;

        var payment = CustomerPayment.Create(
            companyId.Value,
            customer.Id.Value,
            session.BranchId.Value,
            method,
            amount,
            command.Date,
            command.Reference,
            command.Notes,
            userId.Value);

        // Recargo de tarjeta por plan del banco (informativo, no altera el monto imputado).
        if (method == SalePaymentMethod.Card && command.CardBankId.HasValue && command.CardCuotas.HasValue)
        {
            var bank = await _bankRepository.GetByIdAsync(command.CardBankId.Value, companyId, cancellationToken);
            var plan = CardSurchargeCalculator.FindPlan(bank, command.CardCuotas.Value);
            if (plan is not null)
            {
                var surchargeAmt = CardSurchargeCalculator.Compute(amount, plan.SurchargePct);
                payment.SetCardData(bank!.Id, plan.Cuotas, plan.SurchargePct, surchargeAmt);
            }
        }

        // Cheque recibido del cliente: entra a cartera referenciando el cobro.
        if (method == SalePaymentMethod.Check && command.Cheque is not null)
        {
            var c = command.Cheque;
            var cheque = Cheque.CreateForCcPayment(
                companyId,
                payment.Id,
                c.BankId,
                c.Numero,
                c.Titular,
                c.CuitDni,
                c.Monto,
                c.FechaEmision,
                c.FechaVencimiento,
                c.Notas);
            await _chequeRepository.AddAsync(cheque, cancellationToken);
            payment.SetChequeId(cheque.Id);
        }

        await _customerPaymentRepository.AddAsync(payment, cancellationToken);

        // Caja: UN asiento por cobro según método (efectivo al esperado; transfer/tarjeta visibles excluidos;
        // cheque/otros visibles con dirección None).
        session.RegisterCustomerPaymentIncome(method, amount, payment.Id, userId);

        // Imputación FIFO a las ventas CC pendientes; el excedente queda como saldo a favor.
        customer.AddCredit(amount);
        var application = await CustomerCreditApplicator.ApplyToPendingCcSalesAsync(
            customer, companyId, _saleRepository, cancellationToken, payment.Id);
        _customerRepository.Update(customer);

        // Confirmar stock de las ventas que pasaron a Paid con estas imputaciones.
        // Reusa las entidades tracked del applicator (ya traen Details) — sin re-fetch por venta.
        foreach (var sale in application.SalesNowPaidEntities)
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
                        "Stock confirmed as sold (CC fully paid via customer payment).",
                        userId),
                    cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var creditAfter = customer.CreditBalance;
        var appliedToSales = Math.Max(0m, creditBefore + amount - creditAfter);
        var creditAdded = Math.Max(0m, creditAfter - creditBefore);

        return Result<AddCustomerPaymentResponse>.Success(new AddCustomerPaymentResponse(
            payment.Id,
            customer.Id.Value,
            amount,
            appliedToSales,
            creditAdded,
            creditAfter,
            application.Imputaciones));
    }
}
