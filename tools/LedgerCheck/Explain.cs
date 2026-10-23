using eiti.Application.Abstractions.Repositories;
using eiti.Application.Common.Accounts;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Purchases;
using eiti.Domain.Sales;
using Microsoft.Extensions.DependencyInjection;

// Explica una cuenta que no cuadra. La diferencia D = extracto − (pendiente − a favor) se parte, sin resto, en:
//
//   documentos  lo que la tarjeta no ve de cada comprobante: compras Pagadas con pendiente distinto de 0
//               (la tarjeta solo suma las Activas), ventas cobradas de más (la tarjeta las toma como 0) y
//               canjes de ventas CC (bajan el pendiente pero no son un haber del extracto).
//   saldo       saldo a favor guardado − saldo que corresponde: la plata del extracto que no quedó imputada a
//               comprobantes vivos, menos el saldo a favor ya aplicado. Si da distinto de 0, el saldo a favor
//               guardado no coincide con los pagos (por ejemplo, saldo perdido o creado de la nada).
//
// Los dos términos salen de las mismas entidades que leen la API y el extracto: no hay reglas duplicadas.
internal static class Explain
{
    public static async Task SupplierAsync(IServiceProvider services, Guid companyId, Guid supplierId,
        AccountLedgerResponse ledger, decimal storedCredit, decimal difference)
    {
        var purchases = await services.GetRequiredService<IPurchaseRepository>()
            .ListAllBySupplierAsync(companyId, supplierId, CancellationToken.None);
        var live = purchases.Where(p => p.Status != PurchaseStatus.Cancelled).ToList();
        var liveIds = live.Select(p => p.Id).ToHashSet();
        var activeRows = purchases.SelectMany(p => p.Payments.Where(r => r.Status == PurchasePaymentStatus.Active)
            .Select(r => (Purchase: p, Row: r))).ToList();

        // Documentos: la tarjeta solo suma el pendiente de las compras Activas.
        var hidden = live.Where(p => p.Status == PurchaseStatus.Paid && p.PendingAmount != 0).ToList();
        var documents = hidden.Sum(p => p.PendingAmount);

        // Saldo a favor: la plata que no quedó en compras vivas, menos lo que ya se aplicó como saldo a favor.
        var appliedCredit = activeRows
            .Where(x => liveIds.Contains(x.Purchase.Id) && x.Row.Method == PurchasePaymentMethod.SupplierCredit
                && x.Row.SupplierPaymentId is null && x.Row.CreditNoteId is null)
            .Sum(x => x.Row.Amount);
        var moneyOnLive = live.Sum(p => p.TotalPaid) - appliedCredit;
        var unapplied = ledger.TotalCredit - moneyOnLive;
        var expectedCredit = unapplied - appliedCredit;
        var creditTerm = storedCredit - expectedCredit;

        Header(difference, documents, creditTerm);

        foreach (var p in hidden)
        {
            Console.WriteLine($"      compra {p.Code}{Doc(p.InvoiceNumber)} · {p.Status} · total {p.GrandTotal:N2} · imputado {p.TotalPaid:N2} · pendiente {p.PendingAmount:N2}");
            foreach (var r in p.Payments.Where(r => r.Status == PurchasePaymentStatus.Active).OrderBy(r => r.CreatedAt))
                Console.WriteLine($"          {Day(r.Date)} {r.Method,-14} {r.Amount,16:N2}  {Origin(r.SupplierPaymentId, r.CreditNoteId)}");
        }

        if (creditTerm == 0m)
            return;

        Console.WriteLine($"      saldo a favor guardado {storedCredit:N2} · plata sin imputar a compras vivas {unapplied:N2} − saldo ya aplicado {appliedCredit:N2} = {expectedCredit:N2}");
        foreach (var row in ledger.Rows.Where(r => !r.IsCancelled && r.Kind != "invoice"))
        {
            // Lo que este haber dejó en compras vivas y lo que quedó colgado de compras anuladas.
            var ids = row.Means.Select(m => m.PaymentId).ToHashSet();
            var mine = activeRows.Where(x => (x.Row.SupplierPaymentId is { } sp && ids.Contains(sp))
                || (row.Kind == "credit_note" && x.Row.CreditNoteId == row.Id)
                || (row.Source == "legacy" && x.Row.Id == row.Id)).ToList();
            var onLive = mine.Where(x => liveIds.Contains(x.Purchase.Id)).Sum(x => x.Row.Amount);
            var onCancelled = mine.Where(x => !liveIds.Contains(x.Purchase.Id)).Sum(x => x.Row.Amount);
            if (row.Credit == onLive && onCancelled == 0m)
                continue;
            Console.WriteLine($"          {Day(row.Date)} {row.Code ?? row.Description,-18} haber {row.Credit,16:N2} · en compras vivas {onLive,14:N2} · en compras anuladas {onCancelled,14:N2}");
        }
    }

    public static async Task CustomerAsync(IServiceProvider services, Guid companyId, Guid customerId,
        AccountLedgerResponse ledger, decimal storedCredit, decimal difference)
    {
        var sales = await services.GetRequiredService<ISaleRepository>()
            .ListCcSalesByCustomerAsync(new CompanyId(companyId), new CustomerId(customerId), CancellationToken.None);
        var live = sales.Where(s => s.SaleStatus != SaleStatus.Cancel).ToList();
        var liveIds = live.Select(s => s.Id.Value).ToHashSet();
        var activeRows = sales.SelectMany(s => s.CcPayments.Where(r => r.Status == SaleCcPaymentStatus.Active)
            .Select(r => (Sale: s, Row: r))).ToList();

        // Documentos: la tarjeta toma como 0 una venta cobrada de más, y el canje baja el pendiente sin ser haber.
        var hidden = live.Where(s => s.CcPendingAmount < 0 || s.TradeInAmount != 0).ToList();
        var documents = hidden.Sum(s => Math.Min(0m, s.CcPendingAmount) + s.TradeInAmount);

        var appliedCredit = activeRows
            .Where(x => liveIds.Contains(x.Sale.Id.Value) && x.Row.Method == SalePaymentMethod.CustomerCredit
                && x.Row.CustomerPaymentId is null && x.Row.CreditNoteId is null)
            .Sum(x => x.Row.Amount);
        var moneyOnLive = live.Sum(s => s.CcPaidTotal) - appliedCredit;
        var unapplied = ledger.TotalCredit - moneyOnLive;
        var expectedCredit = unapplied - appliedCredit;
        var creditTerm = storedCredit - expectedCredit;

        Header(difference, documents, creditTerm);

        foreach (var s in hidden)
        {
            Console.WriteLine($"      venta {s.Code} · total {s.TotalAmount:N2} · cobrado {s.CcPaidTotal:N2} · canje {s.TradeInAmount:N2} · pendiente {s.CcPendingAmount:N2}");
            foreach (var r in s.CcPayments.Where(r => r.Status == SaleCcPaymentStatus.Active).OrderBy(r => r.CreatedAt))
                Console.WriteLine($"          {Day(r.Date)} {r.Method,-14} {r.Amount,16:N2}  {Origin(r.CustomerPaymentId, r.CreditNoteId)}");
        }

        if (creditTerm == 0m)
            return;

        Console.WriteLine($"      saldo a favor guardado {storedCredit:N2} · plata sin imputar a ventas vivas {unapplied:N2} − saldo ya aplicado {appliedCredit:N2} = {expectedCredit:N2}");
        foreach (var row in ledger.Rows.Where(r => !r.IsCancelled && r.Kind != "invoice"))
        {
            var ids = row.Means.Select(m => m.PaymentId).ToHashSet();
            var mine = activeRows.Where(x => (x.Row.CustomerPaymentId is { } cp && ids.Contains(cp))
                || (row.Kind == "credit_note" && x.Row.CreditNoteId == row.Id)
                || (row.Source == "legacy" && x.Row.Id.Value == row.Id)).ToList();
            var onLive = mine.Where(x => liveIds.Contains(x.Sale.Id.Value)).Sum(x => x.Row.Amount);
            var onCancelled = mine.Where(x => !liveIds.Contains(x.Sale.Id.Value)).Sum(x => x.Row.Amount);
            if (row.Credit == onLive && onCancelled == 0m)
                continue;
            Console.WriteLine($"          {Day(row.Date)} {row.Code ?? row.Description,-18} haber {row.Credit,16:N2} · en ventas vivas {onLive,14:N2} · en ventas anuladas {onCancelled,14:N2}");
        }
    }

    private static void Header(decimal difference, decimal documents, decimal creditTerm)
    {
        var rest = difference - documents - creditTerm;
        Console.WriteLine($"    = documentos {documents:N2} + saldo a favor {creditTerm:N2}{(rest == 0m ? "" : $" + SIN EXPLICAR {rest:N2}")}");
    }

    private static string Doc(string? invoiceNumber) => string.IsNullOrWhiteSpace(invoiceNumber) ? "" : $" (Fact. {invoiceNumber})";
    private static string Day(DateTime date) => date.ToString("yyyy-MM-dd");
    private static string Origin(Guid? paymentId, Guid? creditNoteId) =>
        paymentId is { } p ? $"pago {p.ToString()[..8]}" : creditNoteId is { } n ? $"nota de crédito {n.ToString()[..8]}" : "cargado en el comprobante / saldo a favor";
}
