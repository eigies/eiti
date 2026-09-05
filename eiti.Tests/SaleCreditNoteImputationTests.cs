using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Xunit;

namespace eiti.Tests;

public class SaleCreditNoteImputationTests
{
    private static Sale CcSale(decimal total) =>
        Sale.CreateCc(
            CompanyId.New(),
            BranchId.New(),
            CustomerId.New(),
            [SaleDetail.Create(ProductId.New(), 1, total)]);

    [Fact]
    public void ApplyCustomerCredit_ConCreditNoteId_DejaElBackLinkEnLaFila()
    {
        var sale = CcSale(100_000m);
        var noteId = Guid.NewGuid();

        sale.ApplyCustomerCredit(30_000m, DateTime.UtcNow, Guid.Empty, "NC", creditNoteId: noteId);

        var row = sale.CcPayments.Single();
        row.CreditNoteId.Should().Be(noteId);
        row.CustomerPaymentId.Should().Be(Guid.Empty);
        row.Method.Should().Be(SalePaymentMethod.CustomerCredit);
    }

    // El corazon del diseño: cada origen deshace SOLO lo suyo.
    [Fact]
    public void RevertCreditNote_NoTocaLasImputacionesDeOtrosOrigenes()
    {
        var sale = CcSale(100_000m);
        var noteId = Guid.NewGuid();
        var otherNoteId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        sale.ApplyCustomerCredit(20_000m, DateTime.UtcNow, Guid.Empty, "NC 1", creditNoteId: noteId);
        sale.ApplyCustomerCredit(30_000m, DateTime.UtcNow, Guid.Empty, "NC 2", creditNoteId: otherNoteId);
        sale.ApplyCustomerCredit(10_000m, DateTime.UtcNow, paymentId, "Cobro");

        sale.RevertCreditNote(noteId);

        var active = sale.CcPayments
            .Where(p => p.Status == SaleCcPaymentStatus.Active)
            .ToList();

        active.Should().HaveCount(2);
        active.Should().NotContain(p => p.CreditNoteId == noteId);
        active.Should().Contain(p => p.CreditNoteId == otherNoteId);
        active.Should().Contain(p => p.CustomerPaymentId == paymentId);
    }

    [Fact]
    public void RevertCreditNote_SinFilasDeEsaNota_DevuelveFalse()
    {
        var sale = CcSale(100_000m);
        sale.ApplyCustomerCredit(20_000m, DateTime.UtcNow, Guid.Empty, "NC", creditNoteId: Guid.NewGuid());

        sale.RevertCreditNote(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void RevertCreditNote_DevuelveLaVentaAPendiente_SiEstabaPaga()
    {
        var sale = CcSale(50_000m);
        var noteId = Guid.NewGuid();

        var becamePaid = sale.ApplyCustomerCredit(50_000m, DateTime.UtcNow, Guid.Empty, "NC", creditNoteId: noteId);
        becamePaid.Should().BeTrue();
        sale.SaleStatus.Should().Be(SaleStatus.Paid);

        var revertedFromPaid = sale.RevertCreditNote(noteId);

        revertedFromPaid.Should().BeTrue();
        sale.SaleStatus.Should().Be(SaleStatus.OnHold);
    }
}
