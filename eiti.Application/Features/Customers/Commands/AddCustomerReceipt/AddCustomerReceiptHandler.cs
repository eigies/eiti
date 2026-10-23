using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Common;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.AddCustomerReceipt;

// Recibo: varios medios (tarjeta, cheques, transferencia, efectivo) en un solo comprobante.
// Todo o nada: un solo SaveChanges al final.
public sealed class AddCustomerReceiptHandler
    : IRequestHandler<AddCustomerReceiptCommand, Result<AddCustomerReceiptResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ICustomerReceiptService _customerReceiptService;
    private readonly IUnitOfWork _unitOfWork;

    public AddCustomerReceiptHandler(
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

    public async Task<Result<AddCustomerReceiptResponse>> Handle(
        AddCustomerReceiptCommand command,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<AddCustomerReceiptResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(command.CustomerId), companyId, cancellationToken);
        if (customer is null)
            return Result<AddCustomerReceiptResponse>.Failure(AddCustomerReceiptErrors.CustomerNotFound);

        // Resolver caja abierta: exige caja propia (drawer asignado) o permiso CashDrawerViewAll.
        var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
            _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
        if (resolve.Status != CashSessionResolveStatus.Resolved)
            return Result<AddCustomerReceiptResponse>.Failure(resolve.Status == CashSessionResolveStatus.NoAssignedDrawer
                ? AddCustomerReceiptErrors.NoAssignedCashDrawer
                : AddCustomerReceiptErrors.NoCashSessionOpen);
        var session = resolve.Session!;

        if (BusinessDay.IsFromPreviousBusinessDay(session.OpenedAt))
            return Result<AddCustomerReceiptResponse>.Failure(AddCustomerReceiptErrors.CashSessionFromPreviousDay);

        var outcome = await _customerReceiptService.CreateAsync(
            customer,
            session,
            command.Date,
            command.Notes,
            command.Lines
                .Select(l => new CustomerPaymentLineInput(l.Method, l.Amount, l.Reference, l.CardBankId, l.CardCuotas, l.Cheque))
                .ToList(),
            companyId,
            userId,
            cancellationToken);
        if (outcome.IsFailure)
            return Result<AddCustomerReceiptResponse>.Failure(outcome.Error);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = outcome.Value;
        return Result<AddCustomerReceiptResponse>.Success(new AddCustomerReceiptResponse(
            created.Receipt.Id,
            created.Receipt.Code,
            customer.Id.Value,
            created.Receipt.TotalAmount,
            created.Payments.Select(p => p.Id).ToList(),
            created.AppliedToSales,
            created.CreditAdded,
            created.CustomerCreditBalance,
            created.Imputaciones));
    }
}
