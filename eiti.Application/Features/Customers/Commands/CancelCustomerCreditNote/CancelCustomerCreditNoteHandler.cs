using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Common;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerCreditNote;

// Anula una nota de crédito: deshace SOLO sus propias imputaciones (las de un cobro o las de
// otra NC quedan intactas), devuelve el crédito no consumido y registra la anulación en caja
// como movimiento neutro.
//
// No reutiliza CustomerPaymentReversal: esa función reintegra efectivo al cajón y devuelve el
// cheque a cartera, y una NC no movió ni una cosa ni la otra.
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

        // Cuánto de la NC llegó a imputarse. Se calcula ANTES de revertir, porque después las
        // filas quedan canceladas y ya no suman.
        var sales = await _saleRepository.ListByCreditNoteIdAsync(companyId, note.Id, cancellationToken);
        var imputedTotal = sales
            .SelectMany(s => s.CcPayments)
            .Where(p => p.CreditNoteId == note.Id && p.Status == SaleCcPaymentStatus.Active)
            .Sum(p => p.Amount);

        // Desimputar devuelve `imputedTotal` al saldo disponible; de ahí hay que sacar lo que
        // aportó ESTA nota. La resta ingenua (note.Amount - imputedTotal) destruía el saldo
        // que el cliente ya tenía antes de emitirla: el applicator consume todo el saldo,
        // no solo el de la nota, así que las imputaciones tagueadas pueden superar su importe.
        var restored = customer.CreditBalance + imputedTotal;
        if (restored < note.Amount)
            return Result<CancelCustomerCreditNoteResponse>.Failure(
                CancelCustomerCreditNoteErrors.CreditAlreadyConsumed);

        foreach (var sale in sales)
        {
            var revertedFromPaid = sale.RevertCreditNote(note.Id);

            if (!revertedFromPaid)
                continue;

            // La venta volvió de Paid a pendiente: el stock vuelve a reservado.
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
                        "Stock reverted to reserved (credit note cancelled).",
                        userId),
                    cancellationToken);
            }
        }

        if (imputedTotal > 0m)
            customer.AddCredit(imputedTotal);

        customer.ConsumeCredit(note.Amount);

        _customerRepository.Update(customer);

        session.RegisterCustomerCreditNoteCancellation(note.Amount, note.Id, note.Code, userId);
        note.Cancel(userId.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CancelCustomerCreditNoteResponse>.Success(
            new CancelCustomerCreditNoteResponse(note.Id, note.Code, customer.CreditBalance));
    }
}
