using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Purchases.Common;
using eiti.Domain.Common;
using eiti.Domain.Purchases;
using MediatR;

namespace eiti.Application.Features.Suppliers.Queries.GetSupplierAccount;

public sealed class GetSupplierAccountHandler
    : IRequestHandler<GetSupplierAccountQuery, Result<GetSupplierAccountResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;
    private readonly ISupplierCreditNoteRepository _creditNoteRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IChequeRepository _chequeRepository;

    public GetSupplierAccountHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        ISupplierPaymentRepository supplierPaymentRepository,
        ISupplierCreditNoteRepository creditNoteRepository,
        IPurchaseRepository purchaseRepository,
        IChequeRepository chequeRepository)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _supplierPaymentRepository = supplierPaymentRepository;
        _creditNoteRepository = creditNoteRepository;
        _purchaseRepository = purchaseRepository;
        _chequeRepository = chequeRepository;
    }

    public async Task<Result<GetSupplierAccountResponse>> Handle(
        GetSupplierAccountQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GetSupplierAccountResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, companyId.Value, cancellationToken);
        if (supplier is null)
            return Result<GetSupplierAccountResponse>.Failure(GetSupplierAccountErrors.SupplierNotFound);

        var purchases = await _purchaseRepository.ListAllBySupplierAsync(companyId.Value, supplier.Id, cancellationToken);
        var payments = await _supplierPaymentRepository.ListBySupplierAsync(companyId.Value, supplier.Id, cancellationToken);

        // Números de cheque para los pagos que endosaron un cheque (1 sola query, evita N+1).
        var chequeIds = payments.Where(p => p.ChequeId.HasValue).Select(p => p.ChequeId!.Value).Distinct().ToList();
        var chequeNumeroById = chequeIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _chequeRepository.ListByIdsAsync(chequeIds, companyId, cancellationToken))
                .ToDictionary(c => c.Id, c => c.Numero);

        var deudaTotal = purchases
            .Where(p => p.Status != PurchaseStatus.Cancelled)
            .Sum(p => p.GrandTotal);
        var saldoPendiente = purchases
            .Where(p => p.Status == PurchaseStatus.Active)
            .Sum(p => p.PendingAmount);
        // Imputaciones de notas de crédito: bajan el pendiente pero NO son plata pagada.
        // Como pagadoTotal se deriva por resta, sin descontarlas diría que se pagó de más.
        var notasCreditoImputadas = purchases
            .SelectMany(p => p.Payments)
            .Where(x => x.CreditNoteId.HasValue && x.Status == PurchasePaymentStatus.Active)
            .Sum(x => x.Amount);
        var pagadoTotal = deudaTotal - saldoPendiente - notasCreditoImputadas;

        // Imputaciones por pago de proveedor: qué facturas cubrió cada pago y por cuánto (solo filas activas).
        var imputacionesByPayment = new Dictionary<Guid, List<SupplierPaymentImputacion>>();
        foreach (var p in purchases)
        {
            foreach (var pay in p.Payments.Where(x =>
                x.SupplierPaymentId.HasValue && x.Status == PurchasePaymentStatus.Active))
            {
                if (!imputacionesByPayment.TryGetValue(pay.SupplierPaymentId!.Value, out var list))
                {
                    list = new List<SupplierPaymentImputacion>();
                    imputacionesByPayment[pay.SupplierPaymentId.Value] = list;
                }

                list.Add(new SupplierPaymentImputacion(p.Id, p.Code, p.InvoiceNumber, pay.Amount));
            }
        }

        var movements = new List<SupplierAccountMovement>();

        foreach (var p in purchases)
        {
            movements.Add(new SupplierAccountMovement(
                "compra",
                p.Id,
                p.CreatedAt,
                string.IsNullOrWhiteSpace(p.InvoiceNumber) ? "Compra" : $"Factura {p.InvoiceNumber}",
                p.Code,
                p.GrandTotal,
                true,
                (int)p.Status,
                p.Status.ToString(),
                null,
                null,
                null,
                null));

            // Pagos legacy (cargados por-compra antes del modelo de cuenta): se muestran como créditos.
            foreach (var legacy in p.Payments.Where(pay =>
                pay.SupplierPaymentId is null &&
                pay.Method != PurchasePaymentMethod.SupplierCredit &&
                pay.Status == PurchasePaymentStatus.Active))
            {
                movements.Add(new SupplierAccountMovement(
                    "pago",
                    legacy.Id,
                    legacy.Date,
                    "Pago (compra)",
                    p.Code,
                    legacy.Amount,
                    false,
                    (int)legacy.Status,
                    legacy.Status.ToString(),
                    legacy.Method.ToString(),
                    null,
                    null,
                    null));
            }
        }

        foreach (var pay in payments)
        {
            var chequeNumero = pay.ChequeId.HasValue && chequeNumeroById.TryGetValue(pay.ChequeId.Value, out var num)
                ? num
                : null;

            // Desglose pago → facturas (solo para pagos activos; al anular las imputaciones se cancelan).
            IReadOnlyList<SupplierPaymentImputacion>? imputaciones = null;
            decimal? sobrante = null;
            if (pay.Status == PurchasePaymentStatus.Active
                && imputacionesByPayment.TryGetValue(pay.Id, out var paymentImputaciones))
            {
                imputaciones = paymentImputaciones;
                sobrante = Math.Max(0m, pay.Amount - paymentImputaciones.Sum(i => i.Amount));
            }
            else if (pay.Status == PurchasePaymentStatus.Active)
            {
                // Pago activo sin imputaciones: todo quedó como saldo a favor.
                imputaciones = [];
                sobrante = pay.Amount;
            }

            movements.Add(new SupplierAccountMovement(
                "pago",
                pay.Id,
                pay.Date,
                chequeNumero is not null ? $"Pago · Cheque #{chequeNumero}" : "Pago",
                null,
                pay.Amount,
                false,
                (int)pay.Status,
                pay.Status.ToString(),
                pay.Method.ToString(),
                chequeNumero,
                imputaciones,
                sobrante,
                pay.Reference,
                pay.Notes));
        }

        var creditNotes = await _creditNoteRepository.ListBySupplierAsync(
            companyId.Value, supplier.Id, cancellationToken);

        var imputacionesByNote = new Dictionary<Guid, List<SupplierPaymentImputacion>>();
        foreach (var p in purchases)
        {
            foreach (var pay in p.Payments.Where(x =>
                x.CreditNoteId.HasValue && x.Status == PurchasePaymentStatus.Active))
            {
                if (!imputacionesByNote.TryGetValue(pay.CreditNoteId!.Value, out var list))
                {
                    list = new List<SupplierPaymentImputacion>();
                    imputacionesByNote[pay.CreditNoteId.Value] = list;
                }

                list.Add(new SupplierPaymentImputacion(p.Id, p.Code, p.InvoiceNumber, pay.Amount));
            }
        }

        foreach (var note in creditNotes.Where(n => n.Status == CreditNoteStatus.Active))
        {
            var imputacionesNota = imputacionesByNote.TryGetValue(note.Id, out var l)
                ? (IReadOnlyList<SupplierPaymentImputacion>)l
                : [];

            movements.Add(new SupplierAccountMovement(
                "nota_credito",
                note.Id,
                note.Date,
                note.Reason,
                note.Code,
                note.Amount,
                false,
                (int)note.Status,
                "Activa",
                null,
                null,
                imputacionesNota,
                decimal.Round(
                    note.Amount - imputacionesNota.Sum(i => i.Amount), 2, MidpointRounding.AwayFromZero),
                null,
                note.Reason));
        }

        var ordered = movements
            .OrderByDescending(m => m.Date)
            .ToList();

        return Result<GetSupplierAccountResponse>.Success(new GetSupplierAccountResponse(
            supplier.Id,
            supplier.Name,
            supplier.Phone,
            supplier.Email,
            deudaTotal,
            pagadoTotal,
            saldoPendiente,
            supplier.CreditBalance,
            ordered));
    }
}
