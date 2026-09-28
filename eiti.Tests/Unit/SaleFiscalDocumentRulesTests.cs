using eiti.Domain.Companies;
using eiti.Domain.Sales;
using FluentAssertions;
using Xunit;

namespace eiti.Tests.Unit;

public class SaleFiscalDocumentRulesTests
{
    private static readonly CompanyId Company = CompanyId.New();
    private static readonly SaleId Sale = SaleId.New();

    private static SaleFiscalDocument Invoice(int sequence, bool authorized = true, long number = 1)
    {
        var document = SaleFiscalDocument.CreateInvoice(Company, Sale, sequence);
        if (authorized)
        {
            document.Authorize(Guid.NewGuid(), "invoiceB", 3, number, "75123", null, null, DateTime.UtcNow);
        }

        return document;
    }

    /// <summary>Emite la NC y aplica su efecto: la factura pasa a Voided (estado guardado).</summary>
    private static SaleFiscalDocument CreditNoteFor(SaleFiscalDocument invoice, int sequence, bool authorized = true)
    {
        var document = SaleFiscalDocument.CreateCreditNote(Company, Sale, invoice, sequence);
        if (authorized)
        {
            document.Authorize(Guid.NewGuid(), "creditNoteB", 3, 1, "76999", null, null, DateTime.UtcNow);
            invoice.Void();
        }

        return document;
    }

    // ---------- Idempotencia: lo que protege contra emitir dos comprobantes ----------

    [Fact]
    public void RequestId_is_deterministic_not_random()
    {
        var saleId = SaleId.New();

        var first = SaleFiscalDocument.CreateInvoice(Company, saleId, 1);
        var second = SaleFiscalDocument.CreateInvoice(Company, saleId, 1);

        // Dos filas distintas, misma clave: si una se pierde y se reintenta, el servicio fiscal
        // reconoce el pedido y NO emite un segundo comprobante.
        first.RequestId.Should().Be(second.RequestId);
        first.RequestId.Should().Be($"{saleId.Value}:inv:1");
    }

    [Fact]
    public void RequestId_differs_across_attempts_and_kinds()
    {
        var invoice = SaleFiscalDocument.CreateInvoice(Company, Sale, 1);
        Authorized(invoice);

        var retry = SaleFiscalDocument.CreateInvoice(Company, Sale, 2);
        var creditNote = SaleFiscalDocument.CreateCreditNote(Company, Sale, invoice, 1);

        new[] { invoice.RequestId, retry.RequestId, creditNote.RequestId }
            .Should().OnlyHaveUniqueItems();
        creditNote.RequestId.Should().EndWith(":cn:1");
    }

    [Fact]
    public void NextSequence_starts_at_one_and_advances_per_kind()
    {
        SaleFiscalDocumentRules.NextSequence([], SaleFiscalDocumentKind.Invoice).Should().Be(1);

        var invoice = Invoice(1);
        var creditNote = CreditNoteFor(invoice, 1);
        var documents = new[] { invoice, creditNote };

        SaleFiscalDocumentRules.NextSequence(documents, SaleFiscalDocumentKind.Invoice).Should().Be(2);
        SaleFiscalDocumentRules.NextSequence(documents, SaleFiscalDocumentKind.CreditNote).Should().Be(2);
    }

    // Un intento en vuelo se RE-ENVÍA, no se duplica: ahí está la protección real.
    [Fact]
    public void InFlight_finds_the_attempt_pending_resolution()
    {
        var rejected = Invoice(1, authorized: false);
        rejected.Reject(Guid.NewGuid(), "faltaba el CUIT");
        var pending = Invoice(2, authorized: false);

        var inFlight = SaleFiscalDocumentRules.InFlight([rejected, pending], SaleFiscalDocumentKind.Invoice);

        inFlight.Should().BeSameAs(pending);
    }

    [Fact]
    public void InFlight_is_null_when_nothing_is_pending()
    {
        SaleFiscalDocumentRules.InFlight([Invoice(1)], SaleFiscalDocumentKind.Invoice).Should().BeNull();
    }

    // ---------- Vigencia: la regla que reemplaza al viejo índice único ----------

    [Fact]
    public void LiveInvoice_is_the_authorized_one()
    {
        var rejected = Invoice(1, authorized: false);
        rejected.Reject(Guid.NewGuid(), "error");
        var authorized = Invoice(2, number: 7);

        SaleInvoicingView.From([rejected, authorized]).LiveInvoice!.Number.Should().Be(7);
    }

    // La anulación es un ESTADO, no una deducción: la factura queda Voided.
    [Fact]
    public void An_invoice_reversed_by_a_credit_note_is_voided_and_no_longer_live()
    {
        var invoice = Invoice(1);
        var creditNote = CreditNoteFor(invoice, 1);
        var view = SaleInvoicingView.From([invoice, creditNote]);

        invoice.Status.Should().Be(SaleInvoicingStatus.Voided);
        view.LiveInvoice.Should().BeNull();
        view.CanInvoice.Should().BeTrue();
    }

    // Una NC rechazada no anula nada: la factura sigue vigente.
    [Fact]
    public void A_rejected_credit_note_does_not_void_the_invoice()
    {
        var invoice = Invoice(1);
        var creditNote = CreditNoteFor(invoice, 1, authorized: false);
        creditNote.Reject(Guid.NewGuid(), "rechazada");
        var view = SaleInvoicingView.From([invoice, creditNote]);

        invoice.Status.Should().Be(SaleInvoicingStatus.Invoiced);
        view.LiveInvoice.Should().BeSameAs(invoice);
        view.CanInvoice.Should().BeFalse();
    }

    // Re-facturación: anulada la primera, la segunda pasa a ser la vigente.
    [Fact]
    public void Re_invoicing_after_a_credit_note_makes_the_new_invoice_live()
    {
        var first = Invoice(1, number: 10);
        var creditNote = CreditNoteFor(first, 1);
        var second = Invoice(2, number: 11);
        var view = SaleInvoicingView.From([first, creditNote, second]);

        view.LiveInvoice!.Number.Should().Be(11);
        view.CanInvoice.Should().BeFalse();
    }

    [Fact]
    public void CanInvoice_is_false_while_an_attempt_is_in_progress()
    {
        SaleInvoicingView.From([Invoice(1, authorized: false)]).CanInvoice.Should().BeFalse();
    }

    [Fact]
    public void The_view_falls_back_to_the_last_attempt_so_a_rejection_stays_visible()
    {
        var rejected = Invoice(1, authorized: false);
        rejected.Reject(Guid.NewGuid(), "faltaba el CUIT");

        var view = SaleInvoicingView.From([rejected]);

        view.Status.Should().Be(SaleInvoicingStatus.Rejected);
        view.Invoice!.RejectionReason.Should().Be("faltaba el CUIT");
        view.LiveInvoice.Should().BeNull();
    }

    [Fact]
    public void An_empty_set_reads_as_not_invoiced()
    {
        var view = SaleInvoicingView.From([]);

        view.Status.Should().Be(SaleInvoicingStatus.NotInvoiced);
        view.CanInvoice.Should().BeTrue();
        view.Invoice.Should().BeNull();
    }

    [Fact]
    public void Void_is_idempotent_and_only_applies_to_invoices()
    {
        var invoice = Invoice(1);
        invoice.Void();
        invoice.Void();
        invoice.Status.Should().Be(SaleInvoicingStatus.Voided);

        var creditNote = SaleFiscalDocument.CreateCreditNote(Company, Sale, Invoice(2), 1);
        var act = () => creditNote.Void();
        act.Should().Throw<InvalidOperationException>();
    }

    // Una factura anulada sigue siendo un documento legal: se tiene que poder reimprimir.
    [Fact]
    public void A_voided_invoice_is_still_printable()
    {
        var invoice = Invoice(1);
        var creditNote = CreditNoteFor(invoice, 1);
        var view = SaleInvoicingView.From([invoice, creditNote]);

        view.LiveInvoice.Should().BeNull();
        view.PrintableInvoice.Should().BeSameAs(invoice);
    }

    [Fact]
    public void A_rejected_attempt_is_not_printable()
    {
        var rejected = Invoice(1, authorized: false);
        rejected.Reject(Guid.NewGuid(), "error");

        SaleInvoicingView.From([rejected]).PrintableInvoice.Should().BeNull();
    }

    // Con una anulada y un reintento rechazado, se imprime la que tuvo CAE.
    [Fact]
    public void PrintableInvoice_prefers_the_one_that_was_authorized()
    {
        var voided = Invoice(1, number: 10);
        var creditNote = CreditNoteFor(voided, 1);
        var rejectedRetry = Invoice(2, authorized: false);
        rejectedRetry.Reject(Guid.NewGuid(), "faltaba el CUIT");

        SaleInvoicingView.From([voided, creditNote, rejectedRetry]).PrintableInvoice!.Number.Should().Be(10);
    }

    // ---------- Resultado desconocido: la protección contra doble facturación ----------

    // Si no se sabe qué pasó, el documento NO puede quedar rechazado: el reintento usaría una
    // secuencia nueva y emitiría un segundo comprobante real.
    [Fact]
    public void An_unconfirmed_outcome_stays_in_progress_not_rejected()
    {
        var document = Invoice(1, authorized: false);

        document.MarkUnconfirmed("No se pudo contactar al servicio de facturación.");

        document.Status.Should().Be(SaleInvoicingStatus.InProgress);
        document.RejectionReason.Should().Be("No se pudo contactar al servicio de facturación.");
    }

    // Y por eso el reintento reusa ese intento, con el MISMO RequestId.
    [Fact]
    public void Retrying_an_unconfirmed_attempt_reuses_the_same_request_id()
    {
        var document = Invoice(1, authorized: false);
        document.MarkUnconfirmed("timeout");
        var documents = new[] { document };

        var inFlight = SaleFiscalDocumentRules.InFlight(documents, SaleFiscalDocumentKind.Invoice);

        inFlight.Should().BeSameAs(document);
        inFlight!.RequestId.Should().Be(document.RequestId);
        // Si se hubiera marcado Rechazado, acá saldría 2 y se emitiría un comprobante nuevo.
        SaleFiscalDocumentRules.NextSequence(documents, SaleFiscalDocumentKind.Invoice).Should().Be(2);
    }

    // En cambio un rechazo del fisco SÍ habilita una secuencia nueva: se sabe que no se emitió nada.
    [Fact]
    public void A_fiscal_rejection_allows_a_fresh_attempt()
    {
        var document = Invoice(1, authorized: false);
        document.Reject(Guid.NewGuid(), "faltaba el CUIT");
        var documents = new[] { document };

        SaleFiscalDocumentRules.InFlight(documents, SaleFiscalDocumentKind.Invoice).Should().BeNull();
        SaleInvoicingView.From(documents).CanInvoice.Should().BeTrue();
    }

    [Fact]
    public void MarkUnconfirmed_never_downgrades_an_authorized_document()
    {
        var document = Invoice(1);

        document.MarkUnconfirmed("timeout tardío");

        document.Status.Should().Be(SaleInvoicingStatus.Invoiced);
    }

    // ---------- El requestId se puede volver a parsear: respaldo del callback ----------

    [Theory]
    [InlineData(SaleFiscalDocumentKind.Invoice, 1)]
    [InlineData(SaleFiscalDocumentKind.CreditNote, 3)]
    public void RequestId_round_trips(SaleFiscalDocumentKind kind, int sequence)
    {
        var saleId = SaleId.New();
        var requestId = $"{saleId.Value}:{(kind == SaleFiscalDocumentKind.Invoice ? "inv" : "cn")}:{sequence}";

        SaleFiscalDocument.TryParseRequestId(requestId, out var parsedSale, out var parsedKind, out var parsedSequence)
            .Should().BeTrue();
        parsedSale.Should().Be(saleId);
        parsedKind.Should().Be(kind);
        parsedSequence.Should().Be(sequence);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-guid:inv:1")]
    [InlineData("sin-partes")]
    [InlineData("00000000-0000-0000-0000-000000000001:xx:1")]
    [InlineData("00000000-0000-0000-0000-000000000001:inv:0")]
    public void TryParseRequestId_rejects_garbage(string? requestId)
    {
        SaleFiscalDocument.TryParseRequestId(requestId, out _, out _, out _).Should().BeFalse();
    }

    private static void Authorized(SaleFiscalDocument document) =>
        document.Authorize(Guid.NewGuid(), "invoiceB", 3, 1, "75123", null, null, DateTime.UtcNow);
}
