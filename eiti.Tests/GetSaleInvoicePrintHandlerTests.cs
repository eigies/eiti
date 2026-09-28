using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Queries.GetSaleInvoicePrint;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class GetSaleInvoicePrintHandlerTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _branch;
    private readonly Product _product;
    private readonly Sale _sale;
    private readonly Mock<ISaleFiscalDocumentRepository> _fiscalDocuments = new();
    private readonly Mock<IFiscalizationService> _fiscalization = new();

    public GetSaleInvoicePrintHandlerTests()
    {
        _branch = Branch.Create(_companyId, "Mataderos", "MAT", "Av. Juan B. Alberdi 5000");
        _product = Product.Create(_companyId, "22GD", "22GD", "MOURA", "BATERIA MOURA 12 x 65 22GD", null, 169900m, 98332m, null);
        _sale = Sale.Create(_companyId, _branch.Id, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(_product.Id, 2, 60500m)], code: "MAT-4001");
    }

    [Fact]
    public async Task Invoice_combines_the_authorized_fiscal_data_with_the_sale_items_and_branch()
    {
        var invoice = AuthorizedInvoice();
        _fiscalDocuments.Setup(x => x.ListBySaleAsync(_sale.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync([invoice]);
        _fiscalization.Setup(x => x.GetPrintableDocumentAsync(_companyId.Value, invoice.FiscalDocumentId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Printable("invoiceB", receiver: null));

        var result = await Handler().Handle(new GetSaleInvoicePrintQuery(_sale.Id.Value, SaleFiscalDocumentKind.Invoice), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var print = result.Value;
        print.Kind.Should().Be("invoice");
        (print.Letter, print.TypeCode, print.Title).Should().Be(("B", 6, "FACTURA"));
        print.AuthorizationCode.Should().Be("86390928189972");
        print.Issuer.Cuit.Should().Be("20-39758385-7");
        print.Issuer.VatCondition.Should().Be("IVA Responsable Inscripto");
        print.Issuer.CommercialAddress.Should().Be("Av. Juan B. Alberdi 5000");
        print.Receiver.Should().Be(new SaleInvoicePrintReceiver("Consumidor Final", "Consumidor Final", null));
        print.SaleCode.Should().Be("MAT-4001");
        print.SaleCondition.Should().Be("Contado");
        print.Items.Should().ContainSingle().Which.Should().Be(
            new SaleInvoicePrintItem("MOURA / BATERIA MOURA 12 x 65 22GD", 2, 60500m, 0m, 121000m));
        print.Amounts.Total.Should().Be(121000m);
        print.Amounts.Vat.Should().ContainSingle(x => x.Rate == 21m && x.Amount == 21000m);
        print.IsVoided.Should().BeFalse();
    }

    // El receptor impreso es el que se informó a ARCA (lo devuelve el servicio), no el cliente tal
    // como esté hoy en EITI: si después le cambian el CUIT, la reimpresión sigue coincidiendo con el CAE.
    [Fact]
    public async Task Receiver_comes_from_the_authorized_document()
    {
        var invoice = AuthorizedInvoice();
        _fiscalDocuments.Setup(x => x.ListBySaleAsync(_sale.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync([invoice]);
        _fiscalization.Setup(x => x.GetPrintableDocumentAsync(_companyId.Value, invoice.FiscalDocumentId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Printable("invoiceA", new FiscalReceiver(FiscalReceiverVatCondition.Registered, "CUIT", "30712345678", "Taller SA")));

        var result = await Handler().Handle(new GetSaleInvoicePrintQuery(_sale.Id.Value, SaleFiscalDocumentKind.Invoice), CancellationToken.None);

        result.Value.Letter.Should().Be("A");
        result.Value.Receiver.Should().Be(new SaleInvoicePrintReceiver("Taller SA", "IVA Responsable Inscripto", "CUIT: 30-71234567-8"));
    }

    [Fact]
    public async Task Credit_note_prints_the_authorized_credit_note_with_its_associated_invoice()
    {
        var invoice = AuthorizedInvoice();
        var creditNote = SaleFiscalDocument.CreateCreditNote(_companyId, _sale.Id, invoice, 1);
        creditNote.Authorize(Guid.NewGuid(), "creditNoteB", 1, 2, "86390928300000", new DateTime(2026, 10, 5), "https://qr/cn", DateTime.UtcNow);
        invoice.Void();
        _fiscalDocuments.Setup(x => x.ListBySaleAsync(_sale.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync([invoice, creditNote]);
        var associated = new FiscalAssociatedDocument("invoiceB", 1, 1, new DateOnly(2026, 9, 25));
        _fiscalization.Setup(x => x.GetPrintableDocumentAsync(_companyId.Value, creditNote.FiscalDocumentId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Printable("creditNoteB", receiver: null, associated));

        var result = await Handler().Handle(new GetSaleInvoicePrintQuery(_sale.Id.Value, SaleFiscalDocumentKind.CreditNote), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Kind.Should().Be("creditNote");
        (result.Value.Letter, result.Value.TypeCode, result.Value.Title).Should().Be(("B", 8, "NOTA DE CRÉDITO"));
        result.Value.AssociatedDocument.Should().Be(new SaleInvoicePrintAssociatedDocument("Factura B", 1, 1, new DateOnly(2026, 9, 25)));
    }

    [Fact]
    public async Task Voided_invoice_can_still_be_printed_and_says_so()
    {
        var invoice = AuthorizedInvoice();
        invoice.Void();
        _fiscalDocuments.Setup(x => x.ListBySaleAsync(_sale.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync([invoice]);
        _fiscalization.Setup(x => x.GetPrintableDocumentAsync(_companyId.Value, invoice.FiscalDocumentId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Printable("invoiceB", receiver: null));

        var result = await Handler().Handle(new GetSaleInvoicePrintQuery(_sale.Id.Value, SaleFiscalDocumentKind.Invoice), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsVoided.Should().BeTrue();
    }

    [Fact]
    public async Task Sale_from_another_company_is_not_found()
    {
        var otherCompanySale = Sale.Create(CompanyId.New(), _branch.Id, null, false, SaleStatus.OnHold, [SaleDetail.Create(_product.Id, 1, 100m)]);

        var result = await Handler(otherCompanySale).Handle(
            new GetSaleInvoicePrintQuery(otherCompanySale.Id.Value, SaleFiscalDocumentKind.Invoice), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.InvoicePrint.NotFound");
    }

    [Fact]
    public async Task Sale_without_credit_note_reports_it()
    {
        _fiscalDocuments.Setup(x => x.ListBySaleAsync(_sale.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync([AuthorizedInvoice()]);

        var result = await Handler().Handle(new GetSaleInvoicePrintQuery(_sale.Id.Value, SaleFiscalDocumentKind.CreditNote), CancellationToken.None);

        result.Error.Code.Should().Be("Sales.InvoicePrint.NoCreditNote");
    }

    [Fact]
    public async Task Fiscal_service_failure_is_reported_with_its_message()
    {
        var invoice = AuthorizedInvoice();
        _fiscalDocuments.Setup(x => x.ListBySaleAsync(_sale.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync([invoice]);
        _fiscalization.Setup(x => x.GetPrintableDocumentAsync(_companyId.Value, invoice.FiscalDocumentId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalPrintableDocumentResult(false, ErrorMessage: "No se pudo contactar al servicio de facturación."));

        var result = await Handler().Handle(new GetSaleInvoicePrintQuery(_sale.Id.Value, SaleFiscalDocumentKind.Invoice), CancellationToken.None);

        result.Error.Code.Should().Be("Sales.InvoicePrint.Unavailable");
        result.Error.Description.Should().Be("No se pudo contactar al servicio de facturación.");
    }

    private SaleFiscalDocument AuthorizedInvoice()
    {
        var invoice = SaleFiscalDocument.CreateInvoice(_companyId, _sale.Id, 1);
        invoice.Authorize(Guid.NewGuid(), "invoiceB", 1, 1, "86390928189972", new DateTime(2026, 10, 5), "https://qr", DateTime.UtcNow);
        return invoice;
    }

    private static FiscalPrintableDocumentResult Printable(string type, FiscalReceiver? receiver, FiscalAssociatedDocument? associated = null) =>
        new(true, new FiscalPrintableDocument(
            type, 1, type.StartsWith("credit") ? 2 : 1, new DateOnly(2026, 9, 25), type.StartsWith("credit") ? "86390928300000" : "86390928189972",
            new DateOnly(2026, 10, 5), "https://www.arca.gob.ar/fe/qr/?p=abc",
            new FiscalIssuer("Soler Emiliano Julian", "20397583857", FiscalReceiverVatCondition.Registered, null, new DateOnly(2015, 1, 1)),
            new FiscalAmounts(100000m, [new FiscalVatAmount(21m, 100000m, 21000m)], 0m, 121000m),
            receiver,
            associated));

    private GetSaleInvoicePrintHandler Handler(Sale? sale = null)
    {
        sale ??= _sale;
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);

        var sales = new Mock<ISaleRepository>();
        sales.Setup(x => x.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sale);
        var products = new Mock<IProductRepository>();
        products.Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_product]);
        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(_branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_branch);

        return new GetSaleInvoicePrintHandler(currentUser.Object, sales.Object, _fiscalDocuments.Object,
            products.Object, branches.Object, _fiscalization.Object);
    }
}
