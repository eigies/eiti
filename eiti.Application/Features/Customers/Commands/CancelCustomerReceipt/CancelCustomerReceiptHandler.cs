using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Common;
using eiti.Domain.Cash;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerReceipt;

// Anula un recibo entero: cada medio se revierte como un cobro suelto (las ventas vuelven a pendiente y su
// stock a reservado, los cheques se anulan, el efectivo sale de la caja) y el recibo queda anulado. Todo o nada.
public sealed class CancelCustomerReceiptHandler : IRequestHandler<CancelCustomerReceiptCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerReceiptRepository _customerReceiptRepository;
    private readonly ICustomerPaymentRepository _customerPaymentRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ICustomerReceiptService _customerReceiptService;
    private readonly IUnitOfWork _unitOfWork;

    public CancelCustomerReceiptHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        ICustomerReceiptRepository customerReceiptRepository,
        ICustomerPaymentRepository customerPaymentRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ICustomerReceiptService customerReceiptService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _customerReceiptRepository = customerReceiptRepository;
        _customerPaymentRepository = customerPaymentRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _customerReceiptService = customerReceiptService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelCustomerReceiptCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var receipt = await _customerReceiptRepository.GetByIdAsync(command.ReceiptId, companyId.Value, cancellationToken);
        if (receipt is null || receipt.CustomerId != command.CustomerId)
            return Result.Failure(CancelCustomerReceiptErrors.NotFound);

        if (receipt.Status == SaleCcPaymentStatus.Cancelled)
            return Result.Failure(CancelCustomerReceiptErrors.AlreadyCancelled);

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(receipt.CustomerId), companyId, cancellationToken);
        if (customer is null)
            return Result.Failure(CancelCustomerReceiptErrors.NotFound);

        var payments = await _customerPaymentRepository.ListByReceiptAsync(receipt.Id, companyId.Value, cancellationToken);

        // Si algún medio fue en efectivo hay que sacarlo de la caja; para el resto la reversa neutra (None)
        // se registra en una caja abierta si la hay, para trazabilidad.
        CashSession? session;
        if (payments.Any(p => p.Status == SaleCcPaymentStatus.Active && p.Method == SalePaymentMethod.Cash))
        {
            var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
                _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
            if (resolve.Status == CashSessionResolveStatus.NoAssignedDrawer)
                return Result.Failure(CancelCustomerReceiptErrors.NoAssignedCashDrawer);
            if (resolve.Status == CashSessionResolveStatus.NoSessionOpen)
                return Result.Failure(CancelCustomerReceiptErrors.NoCashSessionOpen);
            session = resolve.Session;
        }
        else
        {
            session = await CashSessionResolver.ResolveOpenSessionBestEffortAsync(
                _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
        }

        await _customerReceiptService.CancelReceiptAsync(
            receipt, payments, customer, session, companyId, userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
