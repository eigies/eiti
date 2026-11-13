using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Purchases.Common;
using eiti.Domain.Common;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierCreditNote;

// Anula una nota de crédito de proveedor sola. La anulación en sí (sus imputaciones, el saldo, la caja) vive en
// SupplierCreditNoteCancellation, compartida con "anular la compra con sus NC".
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

        // El saldo a favor no puede quedar negativo: si el crédito de la nota ya se usó sin su back-link, lo que
        // vuelve al desimputar no alcanza para sacarle su importe.
        var cancellation = await SupplierCreditNoteCancellation.PrepareAsync(
            note, _purchaseRepository, companyId.Value, cancellationToken);
        if (supplier.CreditBalance + cancellation.CreditDelta < 0m)
            return Result<CancelSupplierCreditNoteResponse>.Failure(
                CancelSupplierCreditNoteErrors.CreditAlreadyConsumed);

        cancellation.Apply(supplier, session, userId);
        _supplierRepository.Update(supplier);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CancelSupplierCreditNoteResponse>.Success(
            new CancelSupplierCreditNoteResponse(note.Id, note.Code, supplier.CreditBalance));
    }
}
