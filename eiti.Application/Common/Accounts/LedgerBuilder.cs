namespace eiti.Application.Common.Accounts;

// Un movimiento de cuenta corriente listo para el extracto. Debe = lo que aumenta la deuda (compras,
// ventas CC); Haber = lo que la baja (pagos, cobros, notas de crédito). Date es fecha de negocio (el día
// del usuario, ver BusinessCalendar); Sequence desempata dentro del mismo día (cuándo se registró).
public sealed record LedgerEntry<T>(
    DateTime Date,
    DateTime Sequence,
    decimal Debit,
    decimal Credit,
    bool IsCancelled,
    T Item);

public sealed record LedgerLine<T>(LedgerEntry<T> Entry, decimal Balance);

public sealed record Ledger<T>(
    decimal OpeningBalance,
    IReadOnlyList<LedgerLine<T>> Lines,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance);

/// <summary>
/// Arma el extracto de una cuenta corriente: saldo anterior al rango, renglones en orden cronológico con el
/// saldo acumulado y totales del período. Saldo positivo = se debe; negativo = saldo a favor. Lo anulado no
/// mueve el saldo: si se pide, aparece con el saldo que traía. Es la misma regla para proveedores y clientes.
/// </summary>
public static class LedgerBuilder
{
    public static Ledger<T> Build<T>(
        IEnumerable<LedgerEntry<T>> entries,
        DateTime? dateFrom,
        DateTime? dateTo,
        bool includeCancelled)
    {
        var from = dateFrom?.Date;
        var to = dateTo?.Date;
        var ordered = entries
            .OrderBy(e => e.Date.Date)
            .ThenBy(e => e.Sequence)
            .ToList();

        var opening = from is null
            ? 0m
            : ordered.Where(e => !e.IsCancelled && e.Date.Date < from).Sum(e => e.Debit - e.Credit);

        var balance = opening;
        var totalDebit = 0m;
        var totalCredit = 0m;
        var lines = new List<LedgerLine<T>>();

        foreach (var entry in ordered)
        {
            var day = entry.Date.Date;
            if (day < from)
                continue;
            if (day > to)
                break;

            if (entry.IsCancelled)
            {
                if (includeCancelled)
                    lines.Add(new LedgerLine<T>(entry, balance));
                continue;
            }

            balance += entry.Debit - entry.Credit;
            totalDebit += entry.Debit;
            totalCredit += entry.Credit;
            lines.Add(new LedgerLine<T>(entry, balance));
        }

        return new Ledger<T>(opening, lines, totalDebit, totalCredit, balance);
    }
}
