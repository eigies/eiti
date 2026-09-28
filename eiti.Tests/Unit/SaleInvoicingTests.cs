using eiti.Domain.Companies;
using eiti.Domain.Sales;
using FluentAssertions;
using Xunit;

namespace eiti.Tests.Unit;

public class SaleFiscalDocumentTests
{
    private static SaleFiscalDocument BuildInvoice() =>
        SaleFiscalDocument.CreateInvoice(CompanyId.New(), SaleId.New(), 1);

    private static void Authorize(SaleFiscalDocument document, Guid? id = null) =>
        document.Authorize(
            id ?? Guid.NewGuid(), "invoiceB", 3, 1045, "75123",
            new DateTime(2026, 9, 23), "https://qr", DateTime.UtcNow);

    [Fact]
    public void A_new_document_starts_in_progress()
    {
        var document = BuildInvoice();

        document.Status.Should().Be(SaleInvoicingStatus.InProgress);
        document.IsAuthorized.Should().BeFalse();
    }

    // El callback del servicio fiscal es at-least-once: puede llegar repetido.
    [Fact]
    public void Authorize_is_idempotent_for_the_same_document()
    {
        var document = BuildInvoice();
        var fiscalId = Guid.NewGuid();

        Authorize(document, fiscalId);
        var firstIssuedAt = document.IssuedAt;

        document.Authorize(fiscalId, "invoiceB", 3, 1045, "75123",
            new DateTime(2026, 9, 23), "https://qr", DateTime.UtcNow.AddDays(1));

        document.IssuedAt.Should().Be(firstIssuedAt);
        document.Status.Should().Be(SaleInvoicingStatus.Invoiced);
    }

    // Un mensaje demorado no puede degradar una autorización ya confirmada.
    [Fact]
    public void Reject_does_not_downgrade_an_authorized_document()
    {
        var document = BuildInvoice();
        Authorize(document);

        document.Reject(Guid.NewGuid(), "llegó tarde");

        document.Status.Should().Be(SaleInvoicingStatus.Invoiced);
        document.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void Queue_does_not_downgrade_an_authorized_document()
    {
        var document = BuildInvoice();
        Authorize(document);

        document.Queue(Guid.NewGuid());

        document.Status.Should().Be(SaleInvoicingStatus.Invoiced);
    }

    [Fact]
    public void CanRetry_only_after_a_rejection()
    {
        var document = BuildInvoice();
        document.CanRetry.Should().BeFalse();

        document.Reject(Guid.NewGuid(), "error");

        document.CanRetry.Should().BeTrue();
    }

    // El punto central: la NC anula una FACTURA, no una venta.
    [Fact]
    public void A_credit_note_references_the_invoice_it_reverses()
    {
        var companyId = CompanyId.New();
        var saleId = SaleId.New();
        var invoice = SaleFiscalDocument.CreateInvoice(companyId, saleId, 1);
        Authorize(invoice);

        var creditNote = SaleFiscalDocument.CreateCreditNote(companyId, saleId, invoice, 1);

        creditNote.ReversedDocumentId.Should().Be(invoice.Id);
        creditNote.SaleId.Should().Be(saleId);
    }

    [Fact]
    public void A_credit_note_cannot_reverse_an_unauthorized_invoice()
    {
        var companyId = CompanyId.New();
        var saleId = SaleId.New();
        var invoice = SaleFiscalDocument.CreateInvoice(companyId, saleId, 1);

        var act = () => SaleFiscalDocument.CreateCreditNote(companyId, saleId, invoice, 1);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RejectionReason_is_truncated_to_fit_the_column()
    {
        var document = BuildInvoice();

        document.Reject(Guid.NewGuid(), new string('x', 900));

        document.RejectionReason!.Length.Should().Be(500);
    }

    [Fact]
    public void Reject_without_a_document_id_keeps_the_previous_one()
    {
        var document = BuildInvoice();
        var fiscalId = Guid.NewGuid();
        document.Queue(fiscalId);

        document.Reject(null, "el fisco rechazó");

        document.FiscalDocumentId.Should().Be(fiscalId);
    }
}

/// <summary>
/// La venta no debe saber nada de facturación: esa responsabilidad vive en
/// <see cref="SaleFiscalDocument"/>. Este test lo deja explícito para que no vuelva a filtrarse.
/// </summary>
public class SaleHasNoFiscalResponsibilityTests
{
    [Fact]
    public void Sale_exposes_no_invoicing_members()
    {
        var members = typeof(Sale).GetMembers()
            .Select(member => member.Name)
            .Where(name =>
                name.Contains("Invoic", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Fiscal", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Cae", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("CreditNoteStatus", StringComparison.OrdinalIgnoreCase))
            .ToList();

        members.Should().BeEmpty();
    }
}
