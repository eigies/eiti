using eiti.Domain.Companies;
using eiti.Domain.Sales;
using FluentAssertions;
using Xunit;

namespace eiti.Tests.Unit;

/// <summary>
/// Nota de débito para regularizar una NC emitida a otro receptor que el de su factura: la ND neutraliza esa
/// NC y recién ahí se puede emitir la NC correcta sobre la misma factura.
/// </summary>
public class SaleDebitNoteTests
{
    private static readonly CompanyId Company = CompanyId.New();
    private static readonly SaleId Sale = SaleId.New();

    [Fact]
    public void A_debit_note_has_its_own_request_id_and_the_callback_can_parse_it()
    {
        var (_, creditNote) = VoidedInvoiceWithCreditNote();
        var debitNote = SaleFiscalDocument.CreateDebitNote(Company, Sale, creditNote, 1);

        debitNote.RequestId.Should().Be($"{Sale.Value}:dn:1");
        SaleFiscalDocument.TryParseRequestId(debitNote.RequestId, out var saleId, out var kind, out var sequence).Should().BeTrue();
        saleId.Should().Be(Sale);
        kind.Should().Be(SaleFiscalDocumentKind.DebitNote);
        sequence.Should().Be(1);
    }

    [Fact]
    public void A_debit_note_offsets_an_authorized_credit_note_and_references_it()
    {
        var (_, creditNote) = VoidedInvoiceWithCreditNote();

        var debitNote = SaleFiscalDocument.CreateDebitNote(Company, Sale, creditNote, 1);

        debitNote.Kind.Should().Be(SaleFiscalDocumentKind.DebitNote);
        debitNote.ReversedDocumentId.Should().Be(creditNote.Id);
    }

    [Fact]
    public void A_debit_note_cannot_offset_an_invoice_or_a_credit_note_without_cae()
    {
        var invoice = Invoice();
        var pendingCreditNote = SaleFiscalDocument.CreateCreditNote(Company, Sale, invoice, 1);

        FluentActions.Invoking(() => SaleFiscalDocument.CreateDebitNote(Company, Sale, invoice, 1))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => SaleFiscalDocument.CreateDebitNote(Company, Sale, pendingCreditNote, 1))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_credit_note_offset_by_its_debit_note_is_voided()
    {
        var (_, creditNote) = VoidedInvoiceWithCreditNote();

        creditNote.Void();

        creditNote.Status.Should().Be(SaleInvoicingStatus.Voided);
    }

    [Fact]
    public void The_replacing_credit_note_needs_the_previous_one_offset_first()
    {
        var (invoice, creditNote) = VoidedInvoiceWithCreditNote();

        // Con la NC anterior vigente quedarían dos NC sobre la misma factura.
        FluentActions.Invoking(() => SaleFiscalDocument.CreateCreditNoteReplacing(Company, Sale, invoice, creditNote, 2))
            .Should().Throw<InvalidOperationException>();

        creditNote.Void();
        var replacing = SaleFiscalDocument.CreateCreditNoteReplacing(Company, Sale, invoice, creditNote, 2);

        replacing.Kind.Should().Be(SaleFiscalDocumentKind.CreditNote);
        replacing.ReversedDocumentId.Should().Be(invoice.Id);
        replacing.RequestId.Should().Be($"{Sale.Value}:cn:2");
    }

    [Fact]
    public void The_replacing_credit_note_only_reverses_the_invoice_its_offset_credit_note_reversed()
    {
        var (invoice, creditNote) = VoidedInvoiceWithCreditNote();
        creditNote.Void();
        var (otherInvoice, _) = VoidedInvoiceWithCreditNote();

        FluentActions.Invoking(() => SaleFiscalDocument.CreateCreditNoteReplacing(Company, Sale, otherInvoice, creditNote, 2))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => SaleFiscalDocument.CreateCreditNoteReplacing(Company, Sale, Invoice(), creditNote, 2))
            .Should().Throw<InvalidOperationException>("a live invoice is reversed with a regular credit note");
        invoice.Status.Should().Be(SaleInvoicingStatus.Voided);
    }

    [Fact]
    public void The_view_exposes_the_debit_note_and_keeps_the_latest_credit_note()
    {
        var (invoice, creditNote) = VoidedInvoiceWithCreditNote();
        var debitNote = SaleFiscalDocument.CreateDebitNote(Company, Sale, creditNote, 1);
        debitNote.Authorize(Guid.NewGuid(), "debitNoteA", 10, 1, "77001", null, null, DateTime.UtcNow);
        creditNote.Void();
        var replacing = SaleFiscalDocument.CreateCreditNoteReplacing(Company, Sale, invoice, creditNote, 2);

        var view = SaleInvoicingView.From([invoice, creditNote, debitNote, replacing]);

        view.DebitNote.Should().BeSameAs(debitNote);
        view.CreditNote.Should().BeSameAs(replacing);
        view.Status.Should().Be(SaleInvoicingStatus.Voided);
    }

    private static SaleFiscalDocument Invoice()
    {
        var invoice = SaleFiscalDocument.CreateInvoice(Company, Sale, 1);
        invoice.Authorize(Guid.NewGuid(), "invoiceA", 10, 39, "86428718520200", null, null, DateTime.UtcNow);
        return invoice;
    }

    /// <summary>Factura anulada por su NC (el estado en el que queda MAT-3705).</summary>
    private static (SaleFiscalDocument Invoice, SaleFiscalDocument CreditNote) VoidedInvoiceWithCreditNote()
    {
        var invoice = Invoice();
        var creditNote = SaleFiscalDocument.CreateCreditNote(Company, Sale, invoice, 1);
        creditNote.Authorize(Guid.NewGuid(), "creditNoteA", 10, 4, "86439632269256", null, null, DateTime.UtcNow);
        invoice.Void();
        return (invoice, creditNote);
    }
}
