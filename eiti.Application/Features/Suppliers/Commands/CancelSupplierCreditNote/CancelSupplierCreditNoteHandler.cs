using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Common;
using eiti.Domain.Purchases;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierCreditNote;

// Anula una nota de crédito de proveedor: deshace SOLO sus propias imputaciones (las de un pago
// o las de otra NC quedan intactas), devuelve el crédito no consumido y registra la anulación en
// caja como movimiento neutro.
//
// No reutiliza SupplierPaymentReversal: esa función reintegra efectivo al cajón y devuelve el
// cheque a cartera, y una NC no movió ni una cosa ni la otra.
public sealed class CancelSupplierCreditNoteHandler
    : IRequestHandler<CancelSupplierCreditNoteCommand, Result<CancelSupplierCreditNoteResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierCreditNoteRepository _creditNoteRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelSupplierCreditNoteHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        ISupplierCreditNoteRepository creditNoteRepository,
        IPurchaseRepository purchaseRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _creditNoteRepository = creditNoteRepository;
        _purchaseRepository = purchaseRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CancelSupplierCreditNoteResponse>> Handle(
        CancelSupplierCreditNoteCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<CancelSupplierCreditNoteResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var note = await _creditNoteRepository.GetByIdAsync(
            command.CreditNoteId, companyId.Value, cancellationToken);
        if (note is null || note.SupplierId != command.SupplierId)
            return Result<CancelSupplierCreditNoteResponse>.Failure(
                CancelSupplierCreditNoteErrors.NotFound);

        if (note.Status == CreditNoteStatus.Cancelled)
            return Result<CancelSupplierCreditNoteResponse>.Failure(
                CancelSupplierCreditNoteErrors.AlreadyCancelled);

        var supplier = await _supplierRepository.GetByIdAsync(
            note.SupplierId, companyId.Value, cancellationToken);
        if (supplier is null)
            return Result<CancelSupplierCreditNoteResponse>.Failure(
                CancelSupplierCreditNoteErrors.NotFound);

        var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
            _currentUserService, _cashDrawerRepository, _cashSessionRepository,
            userId, companyId, cancellationToken);
        if (resolve.Status != CashSessionResolveStatus.Resolved)
            return Result<CancelSupplierCreditNoteResponse>.Failure(
                resolve.Status == CashSessionResolveStatus.NoAssignedDrawer
                    ? CancelSupplierCreditNoteErrors.NoAssignedCashDrawer
                    : CancelSupplierCreditNoteErrors.NoCashSessionOpen);
        var session = resolve.Session!;

        // Cuánto de la NC llegó a imputarse. Se calcula ANTES de revertir, porque después las
        // filas quedan canceladas y ya no suman.
        var purchases = await _purchaseRepository.ListByCreditNoteIdAsync(
            companyId.Value, note.Id, cancellationToken);
        var imputedTotal = purchases
            .SelectMany(p => p.Payments)
            .Where(p => p.CreditNoteId == note.Id && p.Status == PurchasePaymentStatus.Active)
            .Sum(p => p.Amount);

        // Desimputar devuelve `imputedTotal` al saldo disponible; de ahí hay que sacar lo que
        // aportó ESTA nota. La resta ingenua (note.Amount - imputedTotal) destruía el saldo
        // que el proveedor ya tenía antes de emitirla: el applicator consume todo el saldo,
        // no solo el de la nota, así que las imputaciones tagueadas pueden superar su importe.
        var restored = supplier.CreditBalance + imputedTotal;
        if (restored < note.Amount)
            return Result<CancelSupplierCreditNoteResponse>.Failure(
                CancelSupplierCreditNoteErrors.CreditAlreadyConsumed);

        foreach (var purchase in purchases)
        {
            purchase.RevertCreditNote(note.Id);
        }

        if (imputedTotal > 0m)
            supplier.AddCredit(imputedTotal);

        supplier.ConsumeCredit(note.Amount);

        _supplierRepository.Update(supplier);

        session.RegisterSupplierCreditNoteCancellation(note.Amount, note.Id, note.Code, userId);
        note.Cancel(userId.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CancelSupplierCreditNoteResponse>.Success(
            new CancelSupplierCreditNoteResponse(note.Id, note.Code, supplier.CreditBalance));
    }
}
