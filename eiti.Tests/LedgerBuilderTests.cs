using eiti.Application.Common.Accounts;
using FluentAssertions;

namespace eiti.Tests;

// El ejemplo del cliente: "la factura de 10 trae un saldo de 2: debés 12; abajo un pago de 8: subtotal 4".
public sealed class LedgerBuilderTests
{
    private static readonly DateTime Base = new(2026, 9, 1);

    private static LedgerEntry<string> E(string name, int day, decimal debit, decimal credit, bool cancelled = false, int seq = 0) =>
        new(Base.AddDays(day), Base.AddDays(day).AddMinutes(seq), debit, credit, cancelled, name);

    [Fact]
    public void SaldoAcumulado_RenglonPorRenglon_ConSaldoAnterior()
    {
        var ledger = LedgerBuilder.Build(
            [E("pago", 5, 0, 8), E("factura", 3, 10, 0), E("vieja", -10, 2, 0)],
            dateFrom: Base,
            dateTo: null,
            includeCancelled: false);

        ledger.OpeningBalance.Should().Be(2m);
        ledger.Lines.Select(l => (l.Entry.Item, l.Balance)).Should().Equal(("factura", 12m), ("pago", 4m));
        ledger.TotalDebit.Should().Be(10m);
        ledger.TotalCredit.Should().Be(8m);
        ledger.ClosingBalance.Should().Be(4m);
    }

    [Fact]
    public void MismoDia_OrdenaPorCuandoSeRegistro()
    {
        var ledger = LedgerBuilder.Build(
            [E("segundo", 1, 0, 5, seq: 30), E("primero", 1, 20, 0, seq: 10)],
            null, null, false);

        ledger.Lines.Select(l => (l.Entry.Item, l.Balance)).Should().Equal(("primero", 20m), ("segundo", 15m));
    }

    [Fact]
    public void LoAnulado_NoMueveElSaldo_YSoloApareceSiSePide()
    {
        var entries = new[] { E("factura", 1, 100, 0), E("pago anulado", 2, 0, 30, cancelled: true), E("pago", 3, 0, 40) };

        var sin = LedgerBuilder.Build(entries, null, null, includeCancelled: false);
        var con = LedgerBuilder.Build(entries, null, null, includeCancelled: true);

        sin.Lines.Select(l => l.Entry.Item).Should().Equal("factura", "pago");
        con.Lines.Select(l => (l.Entry.Item, l.Balance)).Should().Equal(("factura", 100m), ("pago anulado", 100m), ("pago", 60m));
        con.ClosingBalance.Should().Be(60m);
        con.TotalCredit.Should().Be(40m);
    }

    [Fact]
    public void LoAnuladoAntesDelRango_NoEntraEnElSaldoAnterior()
    {
        var ledger = LedgerBuilder.Build(
            [E("factura vieja", -5, 50, 0), E("pago viejo anulado", -4, 0, 50, cancelled: true)],
            dateFrom: Base, dateTo: null, includeCancelled: true);

        ledger.OpeningBalance.Should().Be(50m);
        ledger.Lines.Should().BeEmpty();
    }

    [Fact]
    public void Hasta_EsInclusivoYCortaElPeriodo()
    {
        var ledger = LedgerBuilder.Build(
            [E("a", 1, 10, 0), E("b", 2, 10, 0), E("c", 3, 10, 0)],
            dateFrom: Base.AddDays(1), dateTo: Base.AddDays(2).AddHours(15), includeCancelled: false);

        ledger.Lines.Select(l => l.Entry.Item).Should().Equal("a", "b");
        ledger.ClosingBalance.Should().Be(20m);
    }

    [Fact]
    public void SaldoNegativo_EsSaldoAFavor()
    {
        var ledger = LedgerBuilder.Build([E("factura", 1, 100, 0), E("pago", 2, 0, 130)], null, null, false);

        ledger.ClosingBalance.Should().Be(-30m);
    }
}
