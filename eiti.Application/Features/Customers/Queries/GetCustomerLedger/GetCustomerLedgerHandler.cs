using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Accounts;
using eiti.Domain.Cheques;
using eiti.Domain.Common;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.GetCustomerLedger;

// Arma el extracto con lo que ya está guardado (ventas CC, recibos, cobros sin recibo, cobros cargados en la
// venta antes de la cuenta corriente, notas de crédito); no hay nada precalculado. Las imputaciones (filas
// CustomerCredit) no son movimientos: son a qué venta fue cada haber, y se muestran adentro de él.
// Espejo de GetSupplierLedgerHandler.
public sealed class GetCustomerLedgerHandler : IRequestHandler<GetCustomerLedgerQuery, Result<AccountLedgerResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerPaymentRepository _customerPaymentRepository;
    private readonly ICustomerReceiptRepository _customerReceiptRepository;
    private readonly ICustomerCreditNoteRepository _creditNoteRepository;
    private readonly IChequeRepository _chequeRepository;
    private readonly IBankRepository _bankRepository;
    private readonly TimeProvider _timeProvider;

    public GetCustomerLedgerHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        ISaleRepository saleRepository,
        ICustomerPaymentRepository customerPaymentRepository,
        ICustomerReceiptRepository customerReceiptRepository,
        ICustomerCreditNoteRepository creditNoteRepository,
        IChequeRepository chequeRepository,
        IBankRepository bankRepository,
        TimeProvider timeProvider)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _saleRepository = saleRepository;
        _customerPaymentRepository = customerPaymentRepository;
        _customerReceiptRepository = customerReceiptRepository;
        _creditNoteRepository = creditNoteRepository;
        _chequeRepository = chequeRepository;
        _bankRepository = bankRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AccountLedgerResponse>> Handle(GetCustomerLedgerQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<AccountLedgerResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(request.CustomerId), companyId, cancellationToken);
        if (customer is null)
            return Result<AccountLedgerResponse>.Failure(GetCustomerLedgerErrors.CustomerNotFound);

        var sales = await _saleRepository.ListCcSalesByCustomerAsync(companyId, customer.Id, cancellationToken);
        var payments = await _customerPaymentRepository.ListByCustomerAsync(companyId.Value, customer.Id.Value, cancellationToken);
        var receipts = await _customerReceiptRepository.ListByCustomerAsync(companyId.Value, customer.Id.Value, cancellationToken);
        var creditNotes = await _creditNoteRepository.ListByCustomerAsync(companyId.Value, customer.Id.Value, cancellationToken);

        // Datos de los cheques recibidos y de los bancos (cheques, tarjetas y transferencias), sin N+1.
        var chequeIds = payments.Where(p => p.ChequeId.HasValue).Select(p => p.ChequeId!.Value).Distinct().ToList();
        var cheques = chequeIds.Count == 0
            ? new Dictionary<Guid, Cheque>()
            : (await _chequeRepository.ListByIdsAsync(chequeIds, companyId, cancellationToken)).ToDictionary(c => c.Id);
        var bankIds = cheques.Values.Select(c => c.BankId)
            .Concat(payments.Where(p => p.CardBankId.HasValue).Select(p => p.CardBankId!.Value))
            .Concat(payments.Where(p => p.TransferBankId.HasValue).Select(p => p.TransferBankId!.Value))
            .Concat(sales.SelectMany(s => s.CcPayments).Where(r => r.CardBankId.HasValue).Select(r => r.CardBankId!.Value))
            .Distinct()
            .ToList();
        var bankNames = bankIds.Count == 0
            ? new Dictionary<int, string>()
            : (await _bankRepository.GetByIdsAsync(bankIds, companyId, cancellationToken)).ToDictionary(b => b.Id, b => b.Name);

        // Imputaciones: a qué venta fue cada cobro / nota de crédito (solo filas activas).
        var byPayment = new Dictionary<Guid, List<LedgerImputationResponse>>();
        var byNote = new Dictionary<Guid, List<LedgerImputationResponse>>();
        foreach (var sale in sales)
        {
            foreach (var row in sale.CcPayments.Where(r => r.Status == SaleCcPaymentStatus.Active))
            {
                var imputation = new LedgerImputationResponse(sale.Id.Value, sale.Code ?? string.Empty, null, row.Amount);
                if (row.CustomerPaymentId is { } paymentId)
                    Add(byPayment, paymentId, imputation);
                else if (row.CreditNoteId is { } noteId)
                    Add(byNote, noteId, imputation);
            }
        }

        LedgerMeansResponse Means(CustomerPayment payment)
        {
            var (method, label) = PaymentMeansLabels.For(payment.Method);
            var cheque = payment.ChequeId is { } id ? cheques.GetValueOrDefault(id) : null;
            var bankId = cheque?.BankId ?? payment.CardBankId ?? payment.TransferBankId;
            return new LedgerMeansResponse(
                payment.Id,
                method,
                label,
                payment.Amount,
                payment.Reference,
                cheque?.Numero,
                bankId is { } b ? bankNames.GetValueOrDefault(b) : null,
                cheque?.FechaVencimiento,
                payment.CardCuotas,
                payment.CardSurchargeAmt);
        }

        IReadOnlyList<LedgerImputationResponse> ImputationsOf(IEnumerable<CustomerPayment> lines) =>
            lines
                .SelectMany(p => byPayment.GetValueOrDefault(p.Id) ?? [])
                .GroupBy(i => i.DocumentId)
                .Select(g => g.First() with { Amount = g.Sum(i => i.Amount) })
                .ToList();

        var entries = new List<LedgerEntry<LedgerRowResponse>>();

        foreach (var sale in sales)
        {
            var saleCode = sale.Code ?? string.Empty;
            entries.Add(Entry(
                BusinessCalendar.ToBusinessDate(sale.CreatedAt),
                sale.CreatedAt,
                debit: sale.TotalAmount,
                credit: 0m,
                sale.SaleStatus == SaleStatus.Cancel,
                new LedgerRowResponse(
                    "invoice", "sale", sale.Id.Value, default, sale.Code, null, "Venta",
                    0m, 0m, 0m, false, [], [])));

            // Cobros cargados en la venta antes de la cuenta corriente: son haber, sin recibo ni número.
            foreach (var legacy in sale.CcPayments.Where(r =>
                r.CustomerPaymentId is null && r.CreditNoteId is null && r.Method != SalePaymentMethod.CustomerCredit))
            {
                var (method, label) = PaymentMeansLabels.For(legacy.Method);
                entries.Add(Entry(
                    legacy.Date.Date,
                    legacy.CreatedAt,
                    debit: 0m,
                    credit: legacy.Amount,
                    legacy.Status == SaleCcPaymentStatus.Cancelled,
                    new LedgerRowResponse(
                        "payment", "legacy", legacy.Id.Value, default, null, null, $"Cobro en la venta {saleCode}".Trim(),
                        0m, 0m, 0m, false,
                        [new LedgerMeansResponse(
                            legacy.Id.Value, method, label, legacy.Amount, legacy.Notes,
                            BankName: legacy.CardBankId is { } b ? bankNames.GetValueOrDefault(b) : null,
                            CardInstallments: legacy.CardCuotas,
                            CardSurcharge: legacy.CardSurchargeAmt)],
                        [new LedgerImputationResponse(sale.Id.Value, saleCode, null, legacy.Amount)])));
            }
        }

        var paymentsByReceipt = payments
            .Where(p => p.ReceiptId.HasValue)
            .GroupBy(p => p.ReceiptId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.CreatedAt).ToList());

        foreach (var receipt in receipts)
        {
            var lines = paymentsByReceipt.GetValueOrDefault(receipt.Id) ?? [];
            entries.Add(Entry(
                receipt.Date.Date,
                receipt.CreatedAt,
                debit: 0m,
                credit: lines.Sum(p => p.Amount),
                receipt.Status == SaleCcPaymentStatus.Cancelled,
                new LedgerRowResponse(
                    "payment", "receipt", receipt.Id, default, receipt.Code, null, receipt.Notes ?? "Recibo",
                    0m, 0m, 0m, false,
                    lines.Select(Means).ToList(),
                    ImputationsOf(lines.Where(p => p.Status == SaleCcPaymentStatus.Active)))));
        }

        // Cobros anteriores a los recibos: un renglón por cobro.
        foreach (var payment in payments.Where(p => p.ReceiptId is null))
        {
            entries.Add(Entry(
                payment.Date.Date,
                payment.CreatedAt,
                debit: 0m,
                credit: payment.Amount,
                payment.Status == SaleCcPaymentStatus.Cancelled,
                new LedgerRowResponse(
                    "payment", "payment", payment.Id, default, null, null, payment.Notes ?? "Cobro",
                    0m, 0m, 0m, false,
                    [Means(payment)],
                    ImputationsOf(payment.Status == SaleCcPaymentStatus.Active ? [payment] : []))));
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
        var pending = sales
            .Where(s => s.SaleStatus != SaleStatus.Cancel && s.CcPendingAmount > 0)
            .OrderBy(s => s.CreatedAt)
            .Select(s =>
            {
                var date = BusinessCalendar.ToBusinessDate(s.CreatedAt);
                return new PendingDocumentResponse(
                    s.Id.Value, date, s.Code ?? string.Empty, null, s.TotalAmount, s.TotalAmount - s.CcPendingAmount,
                    s.CcPendingAmount, Math.Max(0, (today - date).Days));
            })
            .ToList();

        return Result<AccountLedgerResponse>.Success(new AccountLedgerResponse(
            customer.Id.Value,
            customer.Name,
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
