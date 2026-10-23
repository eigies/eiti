using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Common;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.AddCustomerPayment;

// Cobro suelto de un solo medio. Se registra como un recibo de un medio, así todo cobro nuevo tiene
// número de RC y el extracto los muestra igual.
public sealed class AddCustomerPaymentHandler : IRequestHandler<AddCustomerPaymentCommand, Result<AddCustomerPaymentResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ICustomerReceiptService _customerReceiptService;
    private readonly IUnitOfWork _unitOfWork;

    public AddCustomerPaymentHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ICustomerReceiptService customerReceiptService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _customerReceiptService = customerReceiptService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AddCustomerPaymentResponse>> Handle(AddCustomerPaymentCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<AddCustomerPaymentResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

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

        var c = command.Cheque;
        var outcome = await _customerReceiptService.CreateAsync(
            customer,
            session,
            command.Date,
            command.Notes,
            [
                new CustomerPaymentLineInput(
                    command.Method,
                    command.Amount,
                    command.Reference,
                    command.CardBankId,
                    command.CardCuotas,
                    c is null
                        ? null
                        : new CustomerPaymentChequeInput(
                            c.BankId, c.Numero, c.Titular, c.CuitDni, c.Monto, c.FechaEmision, c.FechaVencimiento, c.Notas))
            ],
            companyId,
            userId,
            cancellationToken);
        if (outcome.IsFailure)
            return Result<AddCustomerPaymentResponse>.Failure(outcome.Error);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = outcome.Value;
        var payment = created.Payments[0];
        return Result<AddCustomerPaymentResponse>.Success(new AddCustomerPaymentResponse(
            payment.Id,
            customer.Id.Value,
            payment.Amount,
            created.AppliedToSales,
            created.CreditAdded,
            created.CustomerCreditBalance,
            created.Imputaciones));
    }
}
