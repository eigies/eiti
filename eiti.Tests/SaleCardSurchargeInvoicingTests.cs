using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Sales.Commands.UpdateSale;
using eiti.Application.Features.Sales.Common;
using eiti.Application.Features.Sales.Queries.GetSaleById;
using eiti.Application.Features.Sales.Queries.GetSaleInvoicePrint;
using eiti.Application.Features.Sales.Queries.ListSales;
using eiti.Domain.Banks;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

/// <summary>
/// La factura se pide por lo que el cliente pagó: mercadería + recargo de la tarjeta, con el IVA contenido.
/// El comprobante guarda el total que se pidió; con eso se sabe si una venta facturada antes de cobrarse
/// quedó con "recargo no facturado" (opción (a) del spec).
/// </summary>
public sealed class SaleCardSurchargeInvoicingTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _branch;
    private readonly Product _battery;
    private readonly Bank _santander;

    public SaleCardSurchargeInvoicingTests()
    {
        _branch = Branch.Create(_companyId, "Mataderos", "MAT", "Av. Juan B. Alberdi 5000");
        _branch.AssignFiscalPointOfSale(FiscalPointOfSale.Create(_companyId, 10));
        _battery = Product.Create(_companyId, "22GD", "22GD", "MOURA", "Bateria 12x65", null, 100000m, 70000m, null);
        _santander = Bank.Create(_companyId, "Santander");
        _santander.UpsertInstallmentPlan(3, 11.06m, active: true);
    }

    // --- Lo que se pide al servicio fiscal ---

    [Fact]
    public async Task Without_card_surcharge_the_invoice_is_requested_exactly_as_before()
    {
        var sale = NewSale(1210m);

        var request = await RequestInvoice(sale);

        request.Amounts.Should().BeEquivalentTo(
            new FiscalAmounts(1000m, [new FiscalVatAmount(21m, 1000m, 210m)], 0m, 1210m));
    }

    // Ejemplo del spec: $100.000 + recargo $6.636 → factura $106.636 con el IVA al 21 % sobre ese total.
    [Fact]
    public async Task The_card_surcharge_is_invoiced_with_its_vat_at_the_sale_rate()
    {
        var sale = NewSale(100000m, cardSurcharge: 6636m);

        var request = await RequestInvoice(sale);

        request.Amounts.Total.Should().Be(106636m);
        request.Amounts.Vat.Should().ContainSingle().Which.Should().Be(new FiscalVatAmount(21m, 88128.93m, 18507.07m));
        request.Amounts.Net.Should().Be(88128.93m);
    }

    // Una venta que viene de un presupuesto con IVA discriminado: el recargo se grava a la alícuota de la venta y el
    // IVA se recalcula sobre el total (Sale.VatAmount es solo el de la mercadería).
    [Fact]
    public async Task The_surcharge_vat_uses_the_sale_rate_and_not_the_merchandise_vat_amount()
    {
        var sale = NewSale(110500m, cardSurcharge: 11050m);
        sale.SetVat(10.5m);

        var request = await RequestInvoice(sale);

        request.Amounts.Should().BeEquivalentTo(
            new FiscalAmounts(110000m, [new FiscalVatAmount(10.5m, 110000m, 11550m)], 0m, 121550m));
    }

    [Fact]
    public async Task The_invoice_keeps_the_total_it_asked_for()
    {
        var sale = NewSale(100000m, cardSurcharge: 6636m);
        var added = new List<SaleFiscalDocument>();

        await RequestInvoice(sale, added: added);

        added.Should().ContainSingle().Which.RequestedTotal.Should().Be(106636m);
    }

    // Un intento en trámite se re-envía con su mismo RequestId: tiene que ser el mismo pedido. Si la venta se cobró
    // en cuotas mientras tanto, ese recargo no entra en esta factura (queda como no facturado).
    [Fact]
    public async Task A_resent_attempt_asks_for_the_same_total_as_the_first_time()
    {
        var sale = NewSale(100000m, cardSurcharge: 6636m);
        var inFlight = SaleFiscalDocument.CreateInvoice(_companyId, sale.Id, 1);
        inFlight.RecordRequestedTotal(100000m);
        inFlight.MarkUnconfirmed("timeout");

        var request = await RequestInvoice(sale, existing: [inFlight]);

        request.RequestId.Should().Be(inFlight.RequestId);
        request.Amounts.Total.Should().Be(100000m);
        inFlight.RequestedTotal.Should().Be(100000m);
    }

    [Fact]
    public void Only_an_invoice_records_the_requested_total()
    {
        var sale = NewSale(1000m);
        var invoice = AuthorizedInvoice(sale, requestedTotal: 1000m);
        var creditNote = SaleFiscalDocument.CreateCreditNote(_companyId, sale.Id, invoice, 1);

        var act = () => creditNote.RecordRequestedTotal(1000m);

        act.Should().Throw<InvalidOperationException>();
    }

    // --- Recargo no facturado ---

    [Fact]
    public void A_sale_invoiced_before_being_charged_in_installments_shows_the_uninvoiced_surcharge()
    {
        var sale = NewSale(100000m, cardSurcharge: 11060m);

        SaleInvoicingView.From([AuthorizedInvoice(sale, requestedTotal: 100000m)])
            .UninvoicedCardSurcharge(sale).Should().Be(11060m);
    }

    // Facturas emitidas antes de este cambio: sin total guardado se facturó la mercadería (el recargo nunca se facturaba).
    [Fact]
    public void An_old_invoice_without_requested_total_counts_as_the_merchandise()
    {
        var sale = NewSale(100000m, cardSurcharge: 11060m);

        SaleInvoicingView.From([AuthorizedInvoice(sale, requestedTotal: null)])
            .UninvoicedCardSurcharge(sale).Should().Be(11060m);
    }

    [Fact]
    public void An_invoice_that_included_the_surcharge_leaves_nothing_uninvoiced()
    {
        var sale = NewSale(100000m, cardSurcharge: 6636m);

        SaleInvoicingView.From([AuthorizedInvoice(sale, requestedTotal: 106636m)])
            .UninvoicedCardSurcharge(sale).Should().Be(0m);
    }

    [Fact]
    public void Without_a_live_invoice_there_is_no_uninvoiced_surcharge()
    {
        var sale = NewSale(100000m, cardSurcharge: 6636m);
        var voided = AuthorizedInvoice(sale, requestedTotal: 100000m);
        voided.Void();
        var inFlight = SaleFiscalDocument.CreateInvoice(_companyId, sale.Id, 1);
        inFlight.RecordRequestedTotal(100000m);

        SaleInvoicingView.Empty.UninvoicedCardSurcharge(sale).Should().Be(0m);
        SaleInvoicingView.From([voided]).UninvoicedCardSurcharge(sale).Should().Be(0m);
        SaleInvoicingView.From([inFlight]).UninvoicedCardSurcharge(sale).Should().Be(0m);
    }

    [Fact]
    public async Task Sale_detail_exposes_the_uninvoiced_surcharge()
    {
        var sale = NewSale(100000m, cardSurcharge: 11060m);
        var fiscal = FiscalDocuments(sale, AuthorizedInvoice(sale, requestedTotal: 100000m));
        var sales = new Mock<ISaleRepository>();
        sales.Setup(x => x.GetByIdWithCcPaymentsAsync(sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sale);
        var products = new Mock<IProductRepository>();
        products.Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_battery]);

        var result = await new GetSaleByIdHandler(CurrentUser().Object, sales.Object, new Mock<ICustomerRepository>().Object,
                products.Object, fiscal.Object, new Mock<IAddressRepository>().Object)
            .Handle(new GetSaleByIdQuery(sale.Id.Value), CancellationToken.None);

        result.Value.UninvoicedCardSurcharge.Should().Be(11060m);
    }

    [Fact]
    public async Task Sales_list_exposes_the_uninvoiced_surcharge()
    {
        var charged = NewSale(100000m, cardSurcharge: 11060m);
        var notInvoiced = NewSale(100000m, cardSurcharge: 11060m);
        var sales = new Mock<ISaleRepository>();
        sales.Setup(x => x.ListByCompanyAsync(_companyId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([charged, notInvoiced]);
        var fiscal = new Mock<ISaleFiscalDocumentRepository>();
        fiscal.Setup(x => x.ListBySaleIdsAsync(It.IsAny<IReadOnlyCollection<SaleId>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([AuthorizedInvoice(charged, requestedTotal: 100000m)]);
        var transport = new Mock<ISaleTransportAssignmentRepository>();
        transport.Setup(x => x.ListBySaleIdsAsync(It.IsAny<IReadOnlyList<SaleId>>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var products = new Mock<IProductRepository>();
        products.Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_battery]);

        var result = await new ListSalesHandler(CurrentUser().Object, sales.Object, products.Object,
                new Mock<ICustomerRepository>().Object, transport.Object, new Mock<IEmployeeRepository>().Object,
                new Mock<IVehicleRepository>().Object, new Mock<IAddressRepository>().Object, fiscal.Object)
            .Handle(new ListSalesQuery(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), null), CancellationToken.None);

        result.Value.Single(item => item.Id == charged.Id.Value).UninvoicedCardSurcharge.Should().Be(11060m);
        result.Value.Single(item => item.Id == notInvoiced.Id.Value).UninvoicedCardSurcharge.Should().Be(0m);
    }

    // --- Guarda de factura emitida ---

    // La factura incluyó el recargo de una tarjeta: sacar ese cobro (o bajarle las cuotas) dejaría la factura por
    // más de lo que paga el cliente.
    [Fact]
    public async Task An_invoiced_card_surcharge_cannot_be_dropped_by_editing_the_payments()
    {
        var card = SaleCardPayment.Charge(_santander, 3, 60000m).CreatePayment(null);
        var sale = Sale.Create(_companyId, _branch.Id, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(_battery.Id, 1, _battery.Price)], [card], cardSurchargeTotal: 6636m);
        var unitOfWork = new Mock<IUnitOfWork>();

        var result = await UpdateHandler(sale, AuthorizedInvoice(sale, requestedTotal: 106636m), unitOfWork).Handle(
            new UpdateSaleCommand(sale.Id.Value, null, (int)SaleStatus.OnHold, false, null,
                [new UpdateSaleDetailItemRequest(_battery.Id.Value, 1)],
                [new UpdateSalePaymentItemRequest((int)SalePaymentMethod.Cash, 60000m, null)], []),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.Update.InvoicedAmountChange");
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Charging_an_invoiced_sale_in_installments_leaves_the_surcharge_uninvoiced()
    {
        var sale = Sale.Create(_companyId, _branch.Id, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(_battery.Id, 1, _battery.Price)]);
        var invoice = AuthorizedInvoice(sale, requestedTotal: 100000m);

        var result = await UpdateHandler(sale, invoice).Handle(
            new UpdateSaleCommand(sale.Id.Value, null, (int)SaleStatus.Paid, false, DrawerId.Value,
                [new UpdateSaleDetailItemRequest(_battery.Id.Value, 1)],
                [new UpdateSalePaymentItemRequest((int)SalePaymentMethod.Card, 100000m, null, CardBankId: _santander.Id, CardCuotas: 3)],
                []),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        SaleInvoicingView.From([invoice]).UninvoicedCardSurcharge(sale).Should().Be(11060m);
    }

    // --- PDF ---

    [Fact]
    public async Task The_invoice_print_shows_the_card_surcharge_line_when_the_invoice_includes_it()
    {
        var card = SaleCardPayment.Charge(_santander, 3, 100000m).CreatePayment(null);
        var sale = Sale.Create(_companyId, _branch.Id, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(_battery.Id, 1, _battery.Price)], [card], cardSurchargeTotal: 11060m);

        var print = await Print(sale, printedTotal: 111060m);

        print.Adjustments.CardSurcharge.Should().Be(11060m);
        print.Adjustments.CardSurchargeLabel.Should().Be("Recargo financiero tarjeta (Santander, 3 cuotas, 11,06%)");
    }

    [Fact]
    public async Task The_invoice_print_has_no_surcharge_line_when_the_invoice_did_not_include_it()
    {
        var card = SaleCardPayment.Charge(_santander, 3, 100000m).CreatePayment(null);
        var sale = Sale.Create(_companyId, _branch.Id, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(_battery.Id, 1, _battery.Price)], [card], cardSurchargeTotal: 11060m);

        var print = await Print(sale, printedTotal: 100000m);

        print.Adjustments.CardSurcharge.Should().Be(0m);
        print.Adjustments.CardSurchargeLabel.Should().BeNull();
    }

    [Fact]
    public async Task The_invoice_print_of_a_sale_without_card_surcharge_is_unchanged()
    {
        var sale = NewSale(100000m);

        var print = await Print(sale, printedTotal: 100000m);

        print.Adjustments.Should().Be(new SaleInvoicePrintAdjustments(100000m, 0m, 0m, null));
    }

    // --- Armado ---

    private static readonly CashDrawerId DrawerId = CashDrawerId.New();

    private Sale NewSale(decimal total, decimal cardSurcharge = 0m) =>
        Sale.Create(_companyId, _branch.Id, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(_battery.Id, 1, total)], cardSurchargeTotal: cardSurcharge);

    private SaleFiscalDocument AuthorizedInvoice(Sale sale, decimal? requestedTotal)
    {
        var invoice = SaleFiscalDocument.CreateInvoice(_companyId, sale.Id, 1);
        if (requestedTotal is not null)
        {
            invoice.RecordRequestedTotal(requestedTotal.Value);
        }

        invoice.Authorize(Guid.NewGuid(), "invoiceB", 10, 1, "86428447195933", DateTime.UtcNow.AddDays(10), "https://qr", DateTime.UtcNow);
        return invoice;
    }

    private Mock<ICurrentUserService> CurrentUser()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        currentUser.SetupGet(x => x.CanViewAllBranches).Returns(true);
        return currentUser;
    }

    private Mock<ISaleFiscalDocumentRepository> FiscalDocuments(Sale sale, params SaleFiscalDocument[] documents)
    {
        var fiscal = new Mock<ISaleFiscalDocumentRepository>();
        fiscal.Setup(x => x.ListBySaleAsync(sale.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(documents);
        fiscal.Setup(x => x.ListBySaleIdsAsync(It.IsAny<IReadOnlyCollection<SaleId>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);
        return fiscal;
    }

    private async Task<FiscalDocumentRequest> RequestInvoice(
        Sale sale,
        IReadOnlyList<SaleFiscalDocument>? existing = null,
        List<SaleFiscalDocument>? added = null)
    {
        FiscalDocumentRequest? sent = null;
        var fiscal = new Mock<IFiscalizationService>();
        fiscal.SetupGet(x => x.IsEnabled).Returns(true);
        fiscal.Setup(x => x.RequestDocumentAsync(It.IsAny<FiscalDocumentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<FiscalDocumentRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(new FiscalDocumentResult(FiscalDocumentOutcome.Unavailable, ErrorMessage: "timeout"));
        var documents = new Mock<ISaleFiscalDocumentRepository>();
        documents.Setup(x => x.ListBySaleAsync(sale.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(existing ?? []);
        documents.Setup(x => x.AddAsync(It.IsAny<SaleFiscalDocument>(), It.IsAny<CancellationToken>()))
            .Callback<SaleFiscalDocument, CancellationToken>((document, _) => added?.Add(document))
            .Returns(Task.CompletedTask);
        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(_branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_branch);

        await new SaleInvoicingService(fiscal.Object, documents.Object, new Mock<ICustomerRepository>().Object,
                new Mock<ICompanyRepository>().Object, branches.Object)
            .InvoiceAsync(sale, CancellationToken.None);

        sent.Should().NotBeNull();
        return sent!;
    }

    private UpdateSaleHandler UpdateHandler(Sale sale, SaleFiscalDocument invoice, Mock<IUnitOfWork>? unitOfWork = null)
    {
        var userId = UserId.New();
        var currentUser = CurrentUser();
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.Setup(x => x.HasPermission(PermissionCodes.SalesPay)).Returns(true);
        var sales = new Mock<ISaleRepository>();
        sales.Setup(x => x.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sale);
        var products = new Mock<IProductRepository>();
        products.Setup(x => x.GetByIdAsync(_battery.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_battery);
        var stock = BranchProductStock.Create(_companyId, _branch.Id, _battery.Id);
        stock.ApplyManualEntry(10);
        stock.Reserve(1);
        var stocks = new Mock<IBranchProductStockRepository>();
        stocks.Setup(x => x.GetOrCreateAsync(_branch.Id, _battery.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(stock);
        var banks = new Mock<IBankRepository>();
        banks.Setup(x => x.GetByIdAsync(_santander.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_santander);
        var sessions = new Mock<ICashSessionRepository>();
        sessions.Setup(x => x.GetOpenForBranchAsync(_branch.Id, DrawerId, _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashSession.Open(_companyId, _branch.Id, DrawerId, userId, 0m, null));
        var creditNotes = new Mock<ICustomerCreditNoteRepository>();
        creditNotes.Setup(x => x.ListBySaleAsync(_companyId.Value, sale.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        return new UpdateSaleHandler(
            currentUser.Object, sales.Object, new Mock<ICustomerRepository>().Object, products.Object,
            stocks.Object, new Mock<IStockMovementRepository>().Object, new Mock<ICashDrawerRepository>().Object,
            sessions.Object, new Mock<ISaleTransportAssignmentRepository>().Object, new Mock<IAddressRepository>().Object,
            banks.Object, FiscalDocuments(sale, invoice).Object, creditNotes.Object,
            new RetailDiscountPolicy(new Mock<ICompanyRepository>().Object), (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    private async Task<SaleInvoicePrintResponse> Print(Sale sale, decimal printedTotal)
    {
        var invoice = AuthorizedInvoice(sale, requestedTotal: printedTotal);
        var fiscal = new Mock<IFiscalizationService>();
        fiscal.Setup(x => x.GetPrintableDocumentAsync(_companyId.Value, invoice.FiscalDocumentId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalPrintableDocumentResult(true, new FiscalPrintableDocument(
                "invoiceB", 10, 1, new DateOnly(2026, 10, 30), "86428447195933", new DateOnly(2026, 11, 9),
                "https://www.arca.gob.ar/fe/qr/?p=abc",
                new FiscalIssuer("Soler Emiliano Julian", "20397583857", FiscalReceiverVatCondition.Registered, null, new DateOnly(2015, 1, 1)),
                new FiscalAmounts(
                    decimal.Round(printedTotal / 1.21m, 2), [new FiscalVatAmount(21m, decimal.Round(printedTotal / 1.21m, 2), printedTotal - decimal.Round(printedTotal / 1.21m, 2))],
                    0m, printedTotal),
                null,
                null)));
        var sales = new Mock<ISaleRepository>();
        sales.Setup(x => x.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sale);
        var products = new Mock<IProductRepository>();
        products.Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_battery]);
        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(_branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_branch);
        var banks = new Mock<IBankRepository>();
        banks.Setup(x => x.GetByIdAsync(_santander.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_santander);

        var result = await new GetSaleInvoicePrintHandler(CurrentUser().Object, sales.Object, FiscalDocuments(sale, invoice).Object,
                products.Object, branches.Object, fiscal.Object, banks.Object)
            .Handle(new GetSaleInvoicePrintQuery(sale.Id.Value, SaleFiscalDocumentKind.Invoice), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        return result.Value;
    }
}
