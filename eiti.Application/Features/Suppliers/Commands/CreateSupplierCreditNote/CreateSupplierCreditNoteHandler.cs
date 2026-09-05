using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Purchases.Common;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CreateSupplierCreditNote;

// Registra una nota de crédito recibida del proveedor: origina saldo a favor y lo imputa FIFO a
// las compras pendientes, igual que un pago pero sin dinero. Queda visible en la caja del día
// como movimiento neutro (no altera el efectivo esperado).
//
// A diferencia del lado cliente, no confirma stock: las compras no reservan stock como las
// ventas de cuenta corriente.
public sealed class CreateSupplierCreditNoteHandler
    : IRequestHandler<CreateSupplierCreditNoteCommand, Result<CreateSupplierCreditNoteResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierCreditNoteRepository _creditNoteRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSupplierCreditNoteHandler(
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

    public async Task<Result<CreateSupplierCreditNoteResponse>> Handle(
        CreateSupplierCreditNoteCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<CreateSupplierCreditNoteResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var supplier = await _supplierRepository.GetByIdAsync(
            command.SupplierId, companyId.Value, cancellationToken);
        if (supplier is null)
            return Result<CreateSupplierCreditNoteResponse>.Failure(
                CreateSupplierCreditNoteErrors.SupplierNotFound);

        // Compra asociada opcional: si viene, tiene que ser de este proveedor y no estar anulada.
        if (command.PurchaseId.HasValue)
        {
            var purchase = await _purchaseRepository.GetByIdAsync(
                command.PurchaseId.Value, companyId.Value, cancellationToken);

            if (purchase is null || purchase.SupplierId != command.SupplierId)
                return Result<CreateSupplierCreditNoteResponse>.Failure(
                    CreateSupplierCreditNoteErrors.PurchaseNotFound);

            if (purchase.Status == PurchaseStatus.Cancelled)
                return Result<CreateSupplierCreditNoteResponse>.Failure(
                    CreateSupplierCreditNoteErrors.PurchaseCancelled);
        }

        var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
            _currentUserService, _cashDrawerRepository, _cashSessionRepository,
            userId, companyId, cancellationToken);
        if (resolve.Status != CashSessionResolveStatus.Resolved)
            return Result<CreateSupplierCreditNoteResponse>.Failure(
                resolve.Status == CashSessionResolveStatus.NoAssignedDrawer
                    ? CreateSupplierCreditNoteErrors.NoAssignedCashDrawer
                    : CreateSupplierCreditNoteErrors.NoCashSessionOpen);
        var session = resolve.Session!;

        if (BusinessDay.IsFromPreviousBusinessDay(session.OpenedAt))
            return Result<CreateSupplierCreditNoteResponse>.Failure(
                CreateSupplierCreditNoteErrors.CashSessionFromPreviousDay);

        var count = await _creditNoteRepository.CountByBranchAsync(
            companyId.Value, session.BranchId.Value, cancellationToken);
        var code = $"NCP-{(count + 1).ToString().PadLeft(3, '0')}";

        var note = SupplierCreditNote.Create(
            companyId.Value,
            supplier.Id,
            session.BranchId.Value,
            code,
            command.Amount,
            command.Reason,
            command.Date,
            command.PurchaseId,
            userId.Value);

        await _creditNoteRepository.AddAsync(note, cancellationToken);

        supplier.AddCredit(note.Amount);

        // Imputación FIFO con back-link a la NC: sin creditNoteId, anularla no podría deshacerla.
        var imputaciones = await SupplierCreditApplicator.ApplyToPendingPurchasesAsync(
            supplier, companyId.Value, _purchaseRepository, excludePurchaseId: null,
            cancellationToken, supplierPaymentId: null, creditNoteId: note.Id);
        _supplierRepository.Update(supplier);

        // Movimiento neutro: visible en la sesión, ExpectedClosingAmount no se mueve.
        session.RegisterSupplierCreditNote(note.Amount, note.Id, note.Code, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var imputado = imputaciones.Sum(i => i.Amount);

        return Result<CreateSupplierCreditNoteResponse>.Success(new CreateSupplierCreditNoteResponse(
            note.Id,
            note.Code,
            note.Amount,
            supplier.CreditBalance,
            imputaciones,
            decimal.Round(note.Amount - imputado, 2, MidpointRounding.AwayFromZero)));
    }
}
