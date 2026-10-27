using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Commands.RegularizeSaleCreditNote;
using eiti.Application.Features.Sales.Commands.RetrySaleCreditNote;
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
/// Reintentar la NC de una venta anulada y regularizar una NC mal emitida: nada fiscal sale sin que el usuario
/// haya visto a quién va.
/// </summary>
public sealed class SaleCreditNoteCommandsTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Sale _sale;
    private readonly Mock<ISaleInvoicingService> _invoicing = new();

    public SaleCreditNoteCommandsTests()
    {
        _sale = Sale.CreateCc(_companyId, BranchId.New(), CustomerId.New(), [SaleDetail.Create(ProductId.New(), 1, 1881102.30m)]);
        _invoicing.SetupGet(x => x.IsEnabled).Returns(true);
    }

    [Fact]
    public async Task Retry_with_a_changed_customer_receiver_needs_confirmation()
    {
        _sale.Cancel();
        Receivers(LiveInvoice(), customerCuit: "30517431431");

        var result = await Retry().Handle(new RetrySaleCreditNoteCommand(_sale.Id.Value), CancellationToken.None);

        result.Error.Code.Should().Be("Sales.CreditNote.InvoiceReceiverChanged");
        _invoicing.Verify(x => x.IssueCreditNoteAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Retry_confirmed_issues_the_credit_note()
    {
        _sale.Cancel();
        var invoice = LiveInvoice();
        Receivers(invoice, customerCuit: "30517431431");
        _invoicing.Setup(x => x.IssueCreditNoteAsync(_sale, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaleInvoicingOutcome(true, SaleInvoicingStatus.Invoiced, invoice));

        var result = await Retry().Handle(new RetrySaleCreditNoteCommand(_sale.Id.Value, ConfirmInvoiceReceiver: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _invoicing.Verify(x => x.IssueCreditNoteAsync(_sale, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Retry_only_applies_to_a_cancelled_sale()
    {
        Receivers(LiveInvoice(), customerCuit: "30682256504");

        var result = await Retry().Handle(new RetrySaleCreditNoteCommand(_sale.Id.Value), CancellationToken.None);

        result.Error.Code.Should().Be("Sales.CreditNote.SaleNotCancelled");
    }

    [Fact]
    public async Task Retry_without_the_fiscal_service_does_not_issue()
    {
        _sale.Cancel();
        _invoicing.Setup(x => x.GetFiscalReceiversAsync(_sale, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SaleFiscalReceivers.Unavailable([], "No se pudo contactar al servicio de facturación."));

        var result = await Retry().Handle(new RetrySaleCreditNoteCommand(_sale.Id.Value, true), CancellationToken.None);

        result.Error.Code.Should().Be("Sales.CreditNote.Unavailable");
        _invoicing.Verify(x => x.IssueCreditNoteAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Regularizing_needs_explicit_confirmation()
    {
        WrongCreditNote();

        var result = await Regularize().Handle(new RegularizeSaleCreditNoteCommand(_sale.Id.Value), CancellationToken.None);

        result.Error.Code.Should().Be("Sales.CreditNoteRegularization.ConfirmationRequired");
        _invoicing.Verify(x => x.RegularizeCreditNoteAsync(It.IsAny<Sale>(), It.IsAny<SaleFiscalReceivers>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Regularizing_confirmed_issues_the_debit_and_credit_notes()
    {
        var receivers = WrongCreditNote();
        _invoicing.Setup(x => x.RegularizeCreditNoteAsync(_sale, receivers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaleCreditNoteRegularization(
                new SaleInvoicingOutcome(true, SaleInvoicingStatus.Invoiced),
                new SaleInvoicingOutcome(true, SaleInvoicingStatus.Invoiced)));

        var result = await Regularize().Handle(new RegularizeSaleCreditNoteCommand(_sale.Id.Value, Confirm: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task A_credit_note_to_the_invoice_receiver_is_not_regularized()
    {
        var invoice = LiveInvoice();
        var creditNote = SaleFiscalDocument.CreateCreditNote(_companyId, _sale.Id, invoice, 1);
        creditNote.Authorize(Guid.NewGuid(), "creditNoteA", 10, 4, "86439632269256", null, null, DateTime.UtcNow);
        invoice.Void();
        _invoicing.Setup(x => x.GetFiscalReceiversAsync(_sale, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaleFiscalReceivers(true, null, [invoice, creditNote], invoice, Printed("invoiceA", "30682256504"),
                Receiver("30682256504"), creditNote, Printed("creditNoteA", "30682256504")));

        var result = await Regularize().Handle(new RegularizeSaleCreditNoteCommand(_sale.Id.Value, Confirm: true), CancellationToken.None);

        result.Error.Code.Should().Be("Sales.CreditNoteRegularization.NothingToRegularize");
    }

    /// <summary>MAT-3705: factura a 30-68225650-4, NC autorizada a 30-51743143-1.</summary>
    private SaleFiscalReceivers WrongCreditNote()
    {
        var invoice = LiveInvoice();
        var creditNote = SaleFiscalDocument.CreateCreditNote(_companyId, _sale.Id, invoice, 1);
        creditNote.Authorize(Guid.NewGuid(), "creditNoteA", 10, 4, "86439632269256", null, null, DateTime.UtcNow);
        invoice.Void();
        var receivers = new SaleFiscalReceivers(true, null, [invoice, creditNote], invoice, Printed("invoiceA", "30682256504"),
            Receiver("30517431431"), creditNote, Printed("creditNoteA", "30517431431"));
        _invoicing.Setup(x => x.GetFiscalReceiversAsync(_sale, It.IsAny<CancellationToken>())).ReturnsAsync(receivers);
        return receivers;
    }

    private SaleFiscalDocument LiveInvoice()
    {
        var invoice = SaleFiscalDocument.CreateInvoice(_companyId, _sale.Id, 1);
        invoice.Authorize(Guid.NewGuid(), "invoiceA", 10, 39, "86428718520200", null, null, DateTime.UtcNow);
        return invoice;
    }

    private void Receivers(SaleFiscalDocument invoice, string customerCuit) =>
        _invoicing.Setup(x => x.GetFiscalReceiversAsync(_sale, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaleFiscalReceivers(true, null, [invoice], invoice, Printed("invoiceA", "30682256504"),
                Receiver(customerCuit), null, null));

    private static FiscalReceiver Receiver(string cuit) =>
        new(FiscalReceiverVatCondition.Registered, "CUIT", cuit, "TRANSPORTE OLIVOS S.A -");

    private static FiscalPrintableDocument Printed(string type, string cuit) =>
        new(type, 10, 39, new DateOnly(2026, 10, 21), "86428718520200", new DateOnly(2026, 10, 31), "https://qr",
            new FiscalIssuer("SOLER EMILIANO JULIAN", "20397583857", FiscalReceiverVatCondition.Registered, null, new DateOnly(2024, 10, 1)),
            new FiscalAmounts(1554630m, [new FiscalVatAmount(21m, 1554630m, 326472.30m)], 0m, 1881102.30m),
            Receiver(cuit),
            null);

    private RetrySaleCreditNoteHandler Retry() =>
        new(CurrentUser(), Sales(), _invoicing.Object, Mock.Of<IUnitOfWork>());

    private RegularizeSaleCreditNoteHandler Regularize() =>
        new(CurrentUser(), Sales(), _invoicing.Object, Mock.Of<IUnitOfWork>());

    private ICurrentUserService CurrentUser()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.CompanyId).Returns(_companyId);
        return user.Object;
    }

    private ISaleRepository Sales()
    {
        var sales = new Mock<ISaleRepository>();
        sales.Setup(x => x.GetByIdAsync(_sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_sale);
        return sales.Object;
    }
}
