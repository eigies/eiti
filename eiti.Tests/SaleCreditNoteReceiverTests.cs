using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Common;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

/// <summary>
/// Caso real (Baterias Soler, MAT-3705): la factura A salió a CUIT 30-68225650-4; después se cambió el CUIT en la
/// ficha a 30-51743143-1 y la NC salió con el CUIT nuevo. La NC anula ESA factura: va a su receptor, que sale del
/// servicio fiscal y no de la ficha.
/// </summary>
public sealed class SaleCreditNoteReceiverTests
{
    private const string InvoiceCuit = "30682256504";
    private const string CurrentCuit = "30517431431";

    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _branch;
    private readonly Customer _customer;
    private readonly Sale _sale;
    private readonly List<SaleFiscalDocument> _documents = [];
    private readonly List<FiscalDocumentRequest> _requests = [];
    private readonly Mock<IFiscalizationService> _fiscal = new();
    private readonly Guid _invoiceId = Guid.NewGuid();
    private readonly Guid _creditNoteId = Guid.NewGuid();

    public SaleCreditNoteReceiverTests()
    {
        _branch = Branch.Create(_companyId, "Mataderos", "MAT", "Murguiondo 1259");
        _branch.AssignFiscalPointOfSale(FiscalPointOfSale.Create(_companyId, 10));
        _customer = Customer.Create(_companyId, "TRANSPORTE OLIVOS S.A", "-", null,
            taxId: CurrentCuit, ivaCondition: IvaCondition.ResponsableInscripto);
        _sale = Sale.Create(_companyId, _branch.Id, _customer.Id, false, SaleStatus.OnHold,
            [SaleDetail.Create(ProductId.New(), 1, 1881102.30m)]);

        _fiscal.SetupGet(x => x.IsEnabled).Returns(true);
        _fiscal.Setup(x => x.GetPrintableDocumentAsync(_companyId.Value, _invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Printed("invoiceA", 39, Registered(InvoiceCuit)));
        _fiscal.Setup(x => x.GetPrintableDocumentAsync(_companyId.Value, _creditNoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Printed("creditNoteA", 4, Registered(CurrentCuit),
                new FiscalAssociatedDocument("invoiceA", 10, 39, new DateOnly(2026, 10, 21))));
        _fiscal.Setup(x => x.RequestDocumentAsync(It.IsAny<FiscalDocumentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<FiscalDocumentRequest, CancellationToken>((request, _) => _requests.Add(request))
            .ReturnsAsync((FiscalDocumentRequest request, CancellationToken _) => Authorized(request));
    }

    [Fact]
    public async Task The_credit_note_goes_to_the_receiver_of_the_invoice_not_to_the_current_customer()
    {
        var invoice = Invoice();

        var outcome = await Service().IssueCreditNoteAsync(_sale, CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        var request = _requests.Should().ContainSingle().Subject;
        request.RequestedType.Should().Be(FiscalRequestedDocumentType.CreditNote);
        request.Receiver!.DocNumber.Should().Be(InvoiceCuit);
        request.AssociatedDocument.Should().Be(new FiscalAssociatedDocument("invoiceA", 10, 39, new DateOnly(2026, 10, 21)));
        request.Amounts.Total.Should().Be(1881102.30m, "the credit note reverses the invoice as it was issued");
        invoice.Status.Should().Be(SaleInvoicingStatus.Voided);
    }

    [Fact]
    public async Task Without_the_receiver_of_the_invoice_the_credit_note_is_not_requested()
    {
        Invoice();
        _fiscal.Setup(x => x.GetPrintableDocumentAsync(_companyId.Value, _invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalPrintableDocumentResult(false, ErrorMessage: "No se pudo contactar al servicio de facturación."));

        var outcome = await Service().IssueCreditNoteAsync(_sale, CancellationToken.None);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Status.Should().Be(SaleInvoicingStatus.Rejected, "nothing was sent, so a retry can open a new attempt");
        _requests.Should().BeEmpty();
        _documents.Single(d => d.Kind == SaleFiscalDocumentKind.CreditNote).RejectionReason.Should().Contain("a quién se emitió");
    }

    [Fact]
    public async Task Receivers_tell_whether_the_customer_still_matches_the_invoice()
    {
        Invoice();

        var changed = await Service().GetFiscalReceiversAsync(_sale, CancellationToken.None);
        _customer.UpdateProfile("TRANSPORTE OLIVOS S.A", "-", null, null, null, "30-68225650-4", IvaCondition.ResponsableInscripto, null);
        var same = await Service().GetFiscalReceiversAsync(_sale, CancellationToken.None);

        changed.IsAvailable.Should().BeTrue();
        changed.InvoiceIsLive.Should().BeTrue();
        changed.CustomerMatchesInvoice.Should().BeFalse();
        same.CustomerMatchesInvoice.Should().BeTrue("the CUIT is compared by its digits");
    }

    [Fact]
    public async Task Regularizing_issues_a_debit_note_to_the_wrong_receiver_and_a_credit_note_to_the_invoice_receiver()
    {
        var invoice = Invoice();
        var wrongCreditNote = CreditNote(invoice);
        var service = Service();
        var receivers = await service.GetFiscalReceiversAsync(_sale, CancellationToken.None);

        receivers.RegularizationStep.Should().Be(SaleCreditNoteRegularizationStep.DebitNote);
        var result = await service.RegularizeCreditNoteAsync(_sale, receivers, CancellationToken.None);

        result.Completed.Should().BeTrue();
        _requests.Should().HaveCount(2);
        var debitNote = _requests[0];
        debitNote.RequestedType.Should().Be(FiscalRequestedDocumentType.DebitNote);
        debitNote.Receiver!.DocNumber.Should().Be(CurrentCuit, "the debit note offsets the credit note for its own receiver");
        debitNote.AssociatedDocument.Should().Be(new FiscalAssociatedDocument("creditNoteA", 10, 4, new DateOnly(2026, 10, 21)));
        var creditNote = _requests[1];
        creditNote.RequestedType.Should().Be(FiscalRequestedDocumentType.CreditNote);
        creditNote.Receiver!.DocNumber.Should().Be(InvoiceCuit);
        creditNote.AssociatedDocument!.Number.Should().Be(39);
        wrongCreditNote.Status.Should().Be(SaleInvoicingStatus.Voided);
        SaleInvoicingView.From(_documents).CreditNote!.Status.Should().Be(SaleInvoicingStatus.Invoiced);
    }

    [Fact]
    public async Task A_queued_debit_note_stops_before_the_new_credit_note()
    {
        var invoice = Invoice();
        var wrongCreditNote = CreditNote(invoice);
        _fiscal.Setup(x => x.RequestDocumentAsync(It.IsAny<FiscalDocumentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<FiscalDocumentRequest, CancellationToken>((request, _) => _requests.Add(request))
            .ReturnsAsync(new FiscalDocumentResult(FiscalDocumentOutcome.Queued, Guid.NewGuid()));
        var service = Service();

        var result = await service.RegularizeCreditNoteAsync(
            _sale, await service.GetFiscalReceiversAsync(_sale, CancellationToken.None), CancellationToken.None);

        result.Completed.Should().BeFalse();
        result.DebitNote!.Status.Should().Be(SaleInvoicingStatus.InProgress);
        result.CreditNote.Should().BeNull("two live credit notes would reverse the same invoice");
        _requests.Should().ContainSingle();
        wrongCreditNote.Status.Should().Be(SaleInvoicingStatus.Invoiced);
    }

    [Fact]
    public async Task Regularizing_again_after_the_debit_note_only_issues_the_credit_note()
    {
        var invoice = Invoice();
        var wrongCreditNote = CreditNote(invoice);
        var debitNoteId = Guid.NewGuid();
        var debitNote = SaleFiscalDocument.CreateDebitNote(_companyId, _sale.Id, wrongCreditNote, 1);
        debitNote.Authorize(debitNoteId, "debitNoteA", 10, 1, "77001", null, null, DateTime.UtcNow);
        wrongCreditNote.Void();
        _documents.Add(debitNote);
        _fiscal.Setup(x => x.GetPrintableDocumentAsync(_companyId.Value, debitNoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Printed("debitNoteA", 1, Registered(CurrentCuit),
                new FiscalAssociatedDocument("creditNoteA", 10, 4, new DateOnly(2026, 10, 27))));
        var service = Service();
        var receivers = await service.GetFiscalReceiversAsync(_sale, CancellationToken.None);

        receivers.RegularizationStep.Should().Be(SaleCreditNoteRegularizationStep.CreditNote);
        receivers.DebitNotePrinted!.Receiver!.DocNumber.Should().Be(CurrentCuit, "the debit note is shown with its receiver");
        var result = await service.RegularizeCreditNoteAsync(_sale, receivers, CancellationToken.None);

        result.Completed.Should().BeTrue();
        var request = _requests.Should().ContainSingle().Subject;
        request.RequestedType.Should().Be(FiscalRequestedDocumentType.CreditNote);
        request.Receiver!.DocNumber.Should().Be(InvoiceCuit);
    }

    [Fact]
    public async Task A_credit_note_to_the_invoice_receiver_needs_no_regularization()
    {
        var invoice = Invoice();
        CreditNote(invoice);
        _fiscal.Setup(x => x.GetPrintableDocumentAsync(_companyId.Value, _creditNoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Printed("creditNoteA", 4, Registered("30-68225650-4")));

        var receivers = await Service().GetFiscalReceiversAsync(_sale, CancellationToken.None);

        receivers.CreditNoteMatchesInvoice.Should().BeTrue();
        receivers.RegularizationStep.Should().Be(SaleCreditNoteRegularizationStep.None);
    }

    private SaleFiscalDocument Invoice()
    {
        var invoice = SaleFiscalDocument.CreateInvoice(_companyId, _sale.Id, 1);
        invoice.Authorize(_invoiceId, "invoiceA", 10, 39, "86428718520200", null, null, new DateTime(2026, 10, 21));
        _documents.Add(invoice);
        return invoice;
    }

    /// <summary>NC autorizada sobre la factura (la que salió al CUIT nuevo): la factura queda anulada.</summary>
    private SaleFiscalDocument CreditNote(SaleFiscalDocument invoice)
    {
        var creditNote = SaleFiscalDocument.CreateCreditNote(_companyId, _sale.Id, invoice, 1);
        creditNote.Authorize(_creditNoteId, "creditNoteA", 10, 4, "86439632269256", null, null, new DateTime(2026, 10, 27));
        invoice.Void();
        _documents.Add(creditNote);
        return creditNote;
    }

    private static FiscalReceiver Registered(string cuit) =>
        new(FiscalReceiverVatCondition.Registered, "CUIT", cuit, "TRANSPORTE OLIVOS S.A -");

    private static FiscalPrintableDocumentResult Printed(
        string type, long number, FiscalReceiver receiver, FiscalAssociatedDocument? associated = null) =>
        new(true, new FiscalPrintableDocument(
            type, 10, number, new DateOnly(2026, 10, 21), "86428718520200", new DateOnly(2026, 10, 31), "https://qr",
            new FiscalIssuer("SOLER EMILIANO JULIAN", "20397583857", FiscalReceiverVatCondition.Registered, "1661719-03", new DateOnly(2024, 10, 1)),
            new FiscalAmounts(1554630m, [new FiscalVatAmount(21m, 1554630m, 326472.30m)], 0m, 1881102.30m),
            receiver,
            associated));

    private static FiscalDocumentResult Authorized(FiscalDocumentRequest request) =>
        new(FiscalDocumentOutcome.Authorized,
            DocumentId: Guid.NewGuid(),
            DocumentType: request.RequestedType == FiscalRequestedDocumentType.DebitNote ? "debitNoteA" : "creditNoteA",
            PointOfSale: 10,
            Number: request.RequestedType == FiscalRequestedDocumentType.DebitNote ? 1 : 5,
            AuthorizationCode: "86440000000000",
            ValidUntil: new DateTime(2026, 11, 7),
            QrUrl: "https://qr");

    private SaleInvoicingService Service()
    {
        var documents = new Mock<ISaleFiscalDocumentRepository>();
        documents.Setup(x => x.ListBySaleAsync(_sale.Id, _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _documents.ToList());
        documents.Setup(x => x.GetByIdAsync(It.IsAny<SaleFiscalDocumentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SaleFiscalDocumentId id, CancellationToken _) => _documents.SingleOrDefault(d => d.Id == id));
        documents.Setup(x => x.AddAsync(It.IsAny<SaleFiscalDocument>(), It.IsAny<CancellationToken>()))
            .Callback<SaleFiscalDocument, CancellationToken>((d, _) => _documents.Add(d))
            .Returns(Task.CompletedTask);
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(x => x.GetByIdAsync(_customer.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_customer);
        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(_branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_branch);

        return new SaleInvoicingService(_fiscal.Object, documents.Object, customers.Object,
            new Mock<ICompanyRepository>().Object, branches.Object);
    }
}
