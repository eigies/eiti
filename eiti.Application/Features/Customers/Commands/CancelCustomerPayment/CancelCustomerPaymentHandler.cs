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

namespace eiti.Application.Features.Customers.Commands.CancelCustomerPayment;

public sealed class CancelCustomerPaymentHandler : IRequestHandler<CancelCustomerPaymentCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerPaymentRepository _customerPaymentRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IChequeRepository _chequeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelCustomerPaymentHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        ICustomerPaymentRepository customerPaymentRepository,
        ISaleRepository saleRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
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
        _chequeRepository = chequeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelCustomerPaymentCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var payment = await _customerPaymentRepository.GetByIdAsync(command.PaymentId, companyId.Value, cancellationToken);
        if (payment is null || payment.CustomerId != command.CustomerId)
            return Result.Failure(CancelCustomerPaymentErrors.PaymentNotFound);

        if (payment.Status == SaleCcPaymentStatus.Cancelled)
            return Result.Failure(CancelCustomerPaymentErrors.AlreadyCancelled);

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(payment.CustomerId), companyId, cancellationToken);
        if (customer is null)
            return Result.Failure(CancelCustomerPaymentErrors.PaymentNotFound);

        // Para efectivo hay que reintegrar a la caja; otros métodos no movieron efectivo.
        CashSession? session = null;
        if (payment.Method == SalePaymentMethod.Cash)
        {
            var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
                _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
            if (resolve.Status == CashSessionResolveStatus.NoAssignedDrawer)
                return Result.Failure(CancelCustomerPaymentErrors.NoAssignedCashDrawer);
            if (resolve.Status == CashSessionResolveStatus.NoSessionOpen)
                return Result.Failure(CancelCustomerPaymentErrors.NoCashSessionOpen);
            session = resolve.Session;
        }
        else
        {
            // Para no-efectivo igual registramos la reversa neutra (None) en una caja abierta para trazabilidad.
            session = await CashSessionResolver.ResolveOpenSessionBestEffortAsync(
                _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
        }

        // Revertir las imputaciones FIFO que generó este cobro (sus ventas vuelven a pendiente).
        var sales = await _saleRepository.ListByCustomerPaymentIdAsync(companyId, payment.Id, cancellationToken);
        var imputedTotal = 0m;
        foreach (var sale in sales)
        {
            imputedTotal += sale.CcPayments
                .Where(p => p.CustomerPaymentId == payment.Id && p.Status == SaleCcPaymentStatus.Active)
                .Sum(p => p.Amount);

            var revertedFromPaid = sale.RevertCustomerCredit(payment.Id);

            if (revertedFromPaid)
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
                            "Stock reverted to reserved (customer payment cancelled).",
                            userId),
                        cancellationToken);
                }
            }
        }

        // Si el cobro generó saldo a favor, la anulación lo revierte.
        var creditGeneratedByPayment = Math.Max(0m, payment.Amount - imputedTotal);
        if (creditGeneratedByPayment > 0m)
        {
            customer.ConsumeCredit(creditGeneratedByPayment);
        }
        _customerRepository.Update(customer);

        // Reversa de caja: el efectivo sale del cajón (Out); el resto reversa neutra (None) para trazabilidad.
        session?.RegisterCustomerPaymentCancellation(payment.Method, payment.Amount, payment.Id, userId);

        // El cheque recibido sale de cartera (Anulado).
        if (payment.Method == SalePaymentMethod.Check && payment.ChequeId.HasValue)
        {
            var cheque = await _chequeRepository.GetByIdAsync(payment.ChequeId.Value, companyId, cancellationToken);
            if (cheque is not null && cheque.Estado == ChequeStatus.EnCartera)
                cheque.TransitionTo(ChequeStatus.Anulado);
        }

        payment.Cancel();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
