using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Common;
using eiti.Domain.Cash;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerPayment;

// Anula un cobro suelto. Si el cobro comparte recibo con otros medios se rechaza: se anula el recibo entero.
// Si era el único medio de su recibo, el recibo queda anulado con él.
public sealed class CancelCustomerPaymentHandler : IRequestHandler<CancelCustomerPaymentCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerPaymentRepository _customerPaymentRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ICustomerReceiptService _customerReceiptService;
    private readonly IUnitOfWork _unitOfWork;

    public CancelCustomerPaymentHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        ICustomerPaymentRepository customerPaymentRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ICustomerReceiptService customerReceiptService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _customerPaymentRepository = customerPaymentRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _customerReceiptService = customerReceiptService;
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

        var reversible = await _customerReceiptService.EnsureReversibleAloneAsync([payment], companyId, cancellationToken);
        if (reversible.IsFailure)
            return reversible;

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

        // Reversa completa del cobro (des-imputa ventas, revierte crédito, caja y cheque). Lógica compartida
        // con "anular venta (anular pagos)".
        await _customerReceiptService.ReversePaymentAsync(
            payment, customer, session, companyId, userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
