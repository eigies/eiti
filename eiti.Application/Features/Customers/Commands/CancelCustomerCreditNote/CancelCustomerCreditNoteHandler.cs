using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Common;
using eiti.Domain.Common;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerCreditNote;

// Anula una nota de crédito de cliente sola. La anulación en sí (sus imputaciones, el stock de las ventas que
// vuelven a pendiente, el saldo, la caja) vive en CustomerCreditNoteCancellation, compartida con "anular la venta
// con sus NC".
public sealed class CancelCustomerCreditNoteHandler
    : IRequestHandler<CancelCustomerCreditNoteCommand, Result<CancelCustomerCreditNoteResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerCreditNoteRepository _creditNoteRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelCustomerCreditNoteHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        ICustomerCreditNoteRepository creditNoteRepository,
        ISaleRepository saleRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _creditNoteRepository = creditNoteRepository;
        _saleRepository = saleRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CancelCustomerCreditNoteResponse>> Handle(
        CancelCustomerCreditNoteCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<CancelCustomerCreditNoteResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var note = await _creditNoteRepository.GetByIdAsync(
            command.CreditNoteId, companyId.Value, cancellationToken);
        if (note is null || note.CustomerId != command.CustomerId)
            return Result<CancelCustomerCreditNoteResponse>.Failure(
                CancelCustomerCreditNoteErrors.NotFound);

        if (note.Status == CreditNoteStatus.Cancelled)
            return Result<CancelCustomerCreditNoteResponse>.Failure(
                CancelCustomerCreditNoteErrors.AlreadyCancelled);

        var customer = await _customerRepository.GetByIdAsync(
            new CustomerId(note.CustomerId), companyId, cancellationToken);
        if (customer is null)
            return Result<CancelCustomerCreditNoteResponse>.Failure(
                CancelCustomerCreditNoteErrors.NotFound);

        var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
            _currentUserService, _cashDrawerRepository, _cashSessionRepository,
            userId, companyId, cancellationToken);
        if (resolve.Status != CashSessionResolveStatus.Resolved)
            return Result<CancelCustomerCreditNoteResponse>.Failure(
                resolve.Status == CashSessionResolveStatus.NoAssignedDrawer
                    ? CancelCustomerCreditNoteErrors.NoAssignedCashDrawer
                    : CancelCustomerCreditNoteErrors.NoCashSessionOpen);
        var session = resolve.Session!;

        // El saldo a favor no puede quedar negativo: si el crédito de la nota ya se usó sin su back-link, lo que
        // vuelve al desimputar no alcanza para sacarle su importe.
        var cancellation = await CustomerCreditNoteCancellation.PrepareAsync(
            note, _saleRepository, companyId, cancellationToken);
        if (customer.CreditBalance + cancellation.CreditDelta < 0m)
            return Result<CancelCustomerCreditNoteResponse>.Failure(
                CancelCustomerCreditNoteErrors.CreditAlreadyConsumed);

        await cancellation.ApplyAsync(
            customer, session, _branchProductStockRepository, _stockMovementRepository, companyId, userId, cancellationToken);
        _customerRepository.Update(customer);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CancelCustomerCreditNoteResponse>.Success(
            new CancelCustomerCreditNoteResponse(note.Id, note.Code, customer.CreditBalance));
    }
}
