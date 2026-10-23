using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Accounts;
using eiti.Domain.Cheques;
using eiti.Domain.Common;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using MediatR;

namespace eiti.Application.Features.Suppliers.Queries.GetSupplierLedger;

// Arma el extracto con lo que ya está guardado (compras, órdenes de pago, pagos sin orden, pagos cargados en
// la compra antes de la cuenta corriente, notas de crédito); no hay nada precalculado. Las imputaciones
// (filas SupplierCredit) no son movimientos: son a qué factura fue cada haber, y se muestran adentro de él.
public sealed class GetSupplierLedgerHandler : IRequestHandler<GetSupplierLedgerQuery, Result<AccountLedgerResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;
    private readonly ISupplierPaymentOrderRepository _supplierPaymentOrderRepository;
    private readonly ISupplierCreditNoteRepository _creditNoteRepository;
    private readonly IChequeRepository _chequeRepository;
    private readonly IBankRepository _bankRepository;
    private readonly TimeProvider _timeProvider;

    public GetSupplierLedgerHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        IPurchaseRepository purchaseRepository,
        ISupplierPaymentRepository supplierPaymentRepository,
        ISupplierPaymentOrderRepository supplierPaymentOrderRepository,
        ISupplierCreditNoteRepository creditNoteRepository,
        IChequeRepository chequeRepository,
        IBankRepository bankRepository,
        TimeProvider timeProvider)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _purchaseRepository = purchaseRepository;
        _supplierPaymentRepository = supplierPaymentRepository;
        _supplierPaymentOrderRepository = supplierPaymentOrderRepository;
        _creditNoteRepository = creditNoteRepository;
        _chequeRepository = chequeRepository;
        _bankRepository = bankRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AccountLedgerResponse>> Handle(GetSupplierLedgerQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<AccountLedgerResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, companyId.Value, cancellationToken);
        if (supplier is null)
            return Result<AccountLedgerResponse>.Failure(GetSupplierLedgerErrors.SupplierNotFound);

        var purchases = await _purchaseRepository.ListAllBySupplierAsync(companyId.Value, supplier.Id, cancellationToken);
        var payments = await _supplierPaymentRepository.ListBySupplierAsync(companyId.Value, supplier.Id, cancellationToken);
        var orders = await _supplierPaymentOrderRepository.ListBySupplierAsync(companyId.Value, supplier.Id, cancellationToken);
        var creditNotes = await _creditNoteRepository.ListBySupplierAsync(companyId.Value, supplier.Id, cancellationToken);

        // Datos de los cheques entregados y sus bancos (2 queries, sin N+1).
        var chequeIds = payments.Where(p => p.ChequeId.HasValue).Select(p => p.ChequeId!.Value).Distinct().ToList();
        var cheques = chequeIds.Count == 0
            ? new Dictionary<Guid, Cheque>()
            : (await _chequeRepository.ListByIdsAsync(chequeIds, companyId, cancellationToken)).ToDictionary(c => c.Id);
        var bankIds = cheques.Values.Select(c => c.BankId).Distinct().ToList();
        var bankNames = bankIds.Count == 0
            ? new Dictionary<int, string>()
            : (await _bankRepository.GetByIdsAsync(bankIds, companyId, cancellationToken)).ToDictionary(b => b.Id, b => b.Name);

        // Imputaciones: a qué compra fue cada pago / nota de crédito (solo filas activas).
        var byPayment = new Dictionary<Guid, List<LedgerImputationResponse>>();
        var byNote = new Dictionary<Guid, List<LedgerImputationResponse>>();
        foreach (var purchase in purchases)
        {
            foreach (var row in purchase.Payments.Where(r => r.Status == PurchasePaymentStatus.Active))
            {
                var imputation = new LedgerImputationResponse(purchase.Id, purchase.Code, purchase.InvoiceNumber, row.Amount);
                if (row.SupplierPaymentId is { } paymentId)
                    Add(byPayment, paymentId, imputation);
                else if (row.CreditNoteId is { } noteId)
                    Add(byNote, noteId, imputation);
            }
        }

        LedgerMeansResponse Means(SupplierPayment payment)
        {
            var (method, label) = PaymentMeansLabels.For(payment.Method);
            var cheque = payment.ChequeId is { } id ? cheques.GetValueOrDefault(id) : null;
            return new LedgerMeansResponse(
                payment.Id,
                method,
                label,
                payment.Amount,
                payment.Reference,
                cheque?.Numero,
                cheque is null ? null : bankNames.GetValueOrDefault(cheque.BankId),
                cheque?.FechaVencimiento);
        }

        IReadOnlyList<LedgerImputationResponse> ImputationsOf(IEnumerable<SupplierPayment> lines) =>
            lines
                .SelectMany(p => byPayment.GetValueOrDefault(p.Id) ?? [])
                .GroupBy(i => i.DocumentId)
                .Select(g => g.First() with { Amount = g.Sum(i => i.Amount) })
                .ToList();

        var entries = new List<LedgerEntry<LedgerRowResponse>>();

        foreach (var purchase in purchases)
        {
            entries.Add(Entry(
                BusinessCalendar.ToBusinessDate(purchase.CreatedAt),
                purchase.CreatedAt,
                debit: purchase.GrandTotal,
                credit: 0m,
                purchase.Status == PurchaseStatus.Cancelled,
                new LedgerRowResponse(
                    "invoice", "purchase", purchase.Id, default, purchase.Code, purchase.InvoiceNumber, "Compra",
                    0m, 0m, 0m, false, [], [])));

            // Pagos cargados en la compra antes de la cuenta corriente: son haber, sin orden ni número.
            foreach (var legacy in purchase.Payments.Where(r =>
                r.SupplierPaymentId is null && r.CreditNoteId is null && r.Method != PurchasePaymentMethod.SupplierCredit))
            {
                var (method, label) = PaymentMeansLabels.For(legacy.Method);
                entries.Add(Entry(
                    legacy.Date.Date,
                    legacy.CreatedAt,
                    debit: 0m,
                    credit: legacy.Amount,
                    legacy.Status == PurchasePaymentStatus.Cancelled,
                    new LedgerRowResponse(
                        "payment", "legacy", legacy.Id, default, null, null, $"Pago en la compra {purchase.Code}",
                        0m, 0m, 0m, false,
                        [new LedgerMeansResponse(legacy.Id, method, label, legacy.Amount, legacy.Reference)],
                        [new LedgerImputationResponse(purchase.Id, purchase.Code, purchase.InvoiceNumber, legacy.Amount)])));
            }
        }

        var paymentsByOrder = payments
            .Where(p => p.PaymentOrderId.HasValue)
            .GroupBy(p => p.PaymentOrderId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.CreatedAt).ToList());

        foreach (var order in orders)
        {
            var lines = paymentsByOrder.GetValueOrDefault(order.Id) ?? [];
            entries.Add(Entry(
                order.Date.Date,
                order.CreatedAt,
                debit: 0m,
                credit: lines.Sum(p => p.Amount),
                order.Status == PurchasePaymentStatus.Cancelled,
                new LedgerRowResponse(
                    "payment", "payment_order", order.Id, default, order.Code, null, order.Notes ?? "Orden de pago",
                    0m, 0m, 0m, false,
                    lines.Select(Means).ToList(),
                    ImputationsOf(lines.Where(p => p.Status == PurchasePaymentStatus.Active)))));
        }

        // Pagos anteriores a las órdenes de pago: un renglón por pago.
        foreach (var payment in payments.Where(p => p.PaymentOrderId is null))
        {
            entries.Add(Entry(
                payment.Date.Date,
                payment.CreatedAt,
                debit: 0m,
                credit: payment.Amount,
                payment.Status == PurchasePaymentStatus.Cancelled,
                new LedgerRowResponse(
                    "payment", "payment", payment.Id, default, null, null, payment.Notes ?? "Pago",
                    0m, 0m, 0m, false,
                    [Means(payment)],
                    ImputationsOf(payment.Status == PurchasePaymentStatus.Active ? [payment] : []))));
        }

        foreach (var note in creditNotes)
        {
            entries.Add(Entry(
                note.Date.Date,
                note.CreatedAt,
                debit: 0m,
                credit: note.Amount,
                note.Status == CreditNoteStatus.Cancelled,
                new LedgerRowResponse(
                    "credit_note", "credit_note", note.Id, default, note.Code, null, note.Reason,
                    0m, 0m, 0m, false, [],
                    byNote.GetValueOrDefault(note.Id) ?? [])));
        }

        var ledger = LedgerBuilder.Build(entries, request.DateFrom, request.DateTo, request.IncludeCancelled);

        var today = BusinessCalendar.ToBusinessDate(_timeProvider.GetUtcNow().UtcDateTime);
        var pending = purchases
            .Where(p => p.Status == PurchaseStatus.Active && p.PendingAmount > 0)
            .OrderBy(p => p.CreatedAt)
            .Select(p =>
            {
                var date = BusinessCalendar.ToBusinessDate(p.CreatedAt);
                return new PendingDocumentResponse(
                    p.Id, date, p.Code, p.InvoiceNumber, p.GrandTotal, p.GrandTotal - p.PendingAmount, p.PendingAmount,
                    Math.Max(0, (today - date).Days));
            })
            .ToList();

        return Result<AccountLedgerResponse>.Success(new AccountLedgerResponse(
            supplier.Id,
            supplier.Name,
            request.DateFrom?.Date,
            request.DateTo?.Date,
            ledger.OpeningBalance,
            ledger.TotalDebit,
            ledger.TotalCredit,
            ledger.ClosingBalance,
            ledger.Lines
                .Select(l => l.Entry.Item with
                {
                    Date = l.Entry.Date,
                    Debit = l.Entry.Debit,
                    Credit = l.Entry.Credit,
                    Balance = l.Balance,
                    IsCancelled = l.Entry.IsCancelled,
                })
                .ToList(),
            pending));
    }

    private static LedgerEntry<LedgerRowResponse> Entry(
        DateTime date, DateTime sequence, decimal debit, decimal credit, bool isCancelled, LedgerRowResponse row) =>
        new(date, sequence, debit, credit, isCancelled, row);

    private static void Add(Dictionary<Guid, List<LedgerImputationResponse>> map, Guid key, LedgerImputationResponse value)
    {
        if (!map.TryGetValue(key, out var list))
            map[key] = list = [];
        list.Add(value);
    }
}
