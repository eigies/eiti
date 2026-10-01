using System.Net;
using System.Text;
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Application.Features.Sales.Common;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace eiti.Tests;

/// <summary>
/// Datos del cliente que el fisco exige para emitir: se validan antes de pedir el comprobante,
/// y lo que rechaza el servicio llega a la venta en términos que el vendedor puede resolver.
/// </summary>
public sealed class SaleInvoicingReceiverTests
{
    private const string ValidCuit = "20-39758385-7";

    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _branch;

    public SaleInvoicingReceiverTests()
    {
        _branch = Branch.Create(_companyId, "Sucursal Centro", "SC", "San Martin 123");
        _branch.AssignFiscalPointOfSale(FiscalPointOfSale.Create(_companyId, 3));
    }

    [Fact]
    public void Counter_sale_and_final_consumer_need_no_customer_data()
    {
        SaleInvoicingReceiverRules.Validate(null).Should().BeNull();
        SaleInvoicingReceiverRules.Validate(Customer(IvaCondition.ConsumidorFinal, taxId: null)).Should().BeNull();
        SaleInvoicingReceiverRules.Validate(Customer(null, taxId: null)).Should().BeNull();
    }

    [Theory]
    [InlineData(IvaCondition.ResponsableInscripto, "Responsable Inscripto")]
    [InlineData(IvaCondition.Monotributo, "Monotributista")]
    public void Registered_and_monotribute_customers_need_a_cuit(IvaCondition condition, string label)
    {
        var error = SaleInvoicingReceiverRules.Validate(Customer(condition, taxId: null));

        error.Should().Be($"Para facturar a Juan Perez ({label}) hay que cargar su CUIT en la ficha del cliente.");
    }

    [Fact]
    public void A_cuit_with_a_wrong_check_digit_is_rejected()
    {
        SaleInvoicingReceiverRules.Validate(Customer(IvaCondition.ResponsableInscripto, "20-39758385-8"))
            .Should().Be("El CUIT de Juan Perez no es válido. Revisalo en la ficha del cliente.");
    }

    [Fact]
    public void A_valid_cuit_passes_with_or_without_dashes()
    {
        SaleInvoicingReceiverRules.Validate(Customer(IvaCondition.ResponsableInscripto, ValidCuit)).Should().BeNull();
        SaleInvoicingReceiverRules.Validate(Customer(IvaCondition.Monotributo, "20397583857")).Should().BeNull();
    }

    [Fact]
    public void Letter_a_needs_a_registered_or_monotribute_customer()
    {
        SaleInvoicingReceiverRules.Validate(null, InvoiceLetter.A)
            .Should().Be("Para hacer Factura A elegí el cliente o dalo de alta con su CUIT y su condición frente al IVA.");
        SaleInvoicingReceiverRules.Validate(Customer(IvaCondition.ConsumidorFinal, null), InvoiceLetter.A)
            .Should().StartWith("Juan Perez no está cargado como Responsable Inscripto ni Monotributista");
        SaleInvoicingReceiverRules.Validate(Customer(IvaCondition.Monotributo, ValidCuit), InvoiceLetter.A).Should().BeNull();
    }

    [Fact]
    public void Letter_a_still_needs_the_cuit()
    {
        SaleInvoicingReceiverRules.Validate(Customer(IvaCondition.ResponsableInscripto, null), InvoiceLetter.A)
            .Should().Contain("hay que cargar su CUIT");
    }

    [Fact]
    public void Letter_b_is_rejected_for_a_registered_customer()
    {
        SaleInvoicingReceiverRules.Validate(Customer(IvaCondition.ResponsableInscripto, ValidCuit), InvoiceLetter.B)
            .Should().Be("Juan Perez es Responsable Inscripto: le corresponde Factura A.");
        SaleInvoicingReceiverRules.Validate(null, InvoiceLetter.B).Should().BeNull();
        SaleInvoicingReceiverRules.Validate(Customer(IvaCondition.Exento, null), InvoiceLetter.B).Should().BeNull();
    }

    [Theory]
    [InlineData("20397583857", true)]
    [InlineData("30500010912", true)]
    [InlineData("20397583858", false)]
    [InlineData("2039758385", false)]
    [InlineData("203975838571", false)]
    public void Cuit_check_digit(string digits, bool valid) =>
        SaleInvoicingReceiverRules.IsValidCuit(digits).Should().Be(valid);

    [Fact]
    public async Task Invoicing_a_registered_customer_without_cuit_is_rejected_without_calling_the_fiscal_service()
    {
        var customer = Customer(IvaCondition.ResponsableInscripto, taxId: null);
        var sale = Sale(customer);
        var fiscal = new Mock<IFiscalizationService>();
        fiscal.SetupGet(x => x.IsEnabled).Returns(true);
        var added = new List<SaleFiscalDocument>();

        var outcome = await InvoicingService(fiscal, customer, [], added).InvoiceAsync(sale, CancellationToken.None);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Status.Should().Be(SaleInvoicingStatus.Rejected);
        outcome.Message.Should().Contain("hay que cargar su CUIT");
        added.Should().ContainSingle().Which.RejectionReason.Should().Be(outcome.Message);
        fiscal.Verify(x => x.RequestDocumentAsync(It.IsAny<FiscalDocumentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_invoice_is_requested_on_the_point_of_sale_of_the_selling_branch()
    {
        var customer = Customer(IvaCondition.ConsumidorFinal, taxId: null);
        var fiscal = new Mock<IFiscalizationService>();
        fiscal.SetupGet(x => x.IsEnabled).Returns(true);
        fiscal.Setup(x => x.RequestDocumentAsync(It.IsAny<FiscalDocumentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalDocumentResult(FiscalDocumentOutcome.Unavailable, ErrorMessage: "timeout"));

        await InvoicingService(fiscal, customer, [], []).InvoiceAsync(Sale(customer), CancellationToken.None);

        fiscal.Verify(x => x.RequestDocumentAsync(
            It.Is<FiscalDocumentRequest>(r => r.PointOfSale == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_branch_without_point_of_sale_is_rejected_without_calling_the_fiscal_service()
    {
        // Facturación automática: la venta ya se hizo, el motivo queda en la venta para resolverlo.
        _branch.ClearFiscalPointOfSale();
        var customer = Customer(IvaCondition.ConsumidorFinal, taxId: null);
        var fiscal = new Mock<IFiscalizationService>();
        fiscal.SetupGet(x => x.IsEnabled).Returns(true);
        var added = new List<SaleFiscalDocument>();

        var outcome = await InvoicingService(fiscal, customer, [], added).InvoiceAsync(Sale(customer), CancellationToken.None);

        outcome.Status.Should().Be(SaleInvoicingStatus.Rejected);
        outcome.Message.Should().StartWith("La sucursal Sucursal Centro no tiene punto de venta");
        added.Should().ContainSingle().Which.RejectionReason.Should().Be(outcome.Message);
        fiscal.Verify(x => x.RequestDocumentAsync(It.IsAny<FiscalDocumentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_attempt_in_flight_is_always_resent_even_if_the_customer_data_became_invalid()
    {
        // El intento pudo haberse emitido: no re-enviarlo dejaría la venta trabada o duplicaría el comprobante.
        var customer = Customer(IvaCondition.ResponsableInscripto, taxId: null);
        var sale = Sale(customer);
        var inFlight = SaleFiscalDocument.CreateInvoice(_companyId, sale.Id, 1);
        inFlight.MarkUnconfirmed("timeout");
        var fiscal = new Mock<IFiscalizationService>();
        fiscal.SetupGet(x => x.IsEnabled).Returns(true);
        fiscal.Setup(x => x.RequestDocumentAsync(It.IsAny<FiscalDocumentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalDocumentResult(FiscalDocumentOutcome.Unavailable, ErrorMessage: "timeout"));

        await InvoicingService(fiscal, customer, [inFlight], []).InvoiceAsync(sale, CancellationToken.None);

        fiscal.Verify(x => x.RequestDocumentAsync(
            It.Is<FiscalDocumentRequest>(r => r.RequestId == inFlight.RequestId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Arca.ReceiverIdentificationRequired", "El monto supera el tope de ARCA")]
    [InlineData("Arca.ReceiverCuitRequired", "hay que cargar su CUIT")]
    [InlineData("Arca.ReceiverIsIssuer", "mismo CUIT que quien factura")]
    public async Task Fiscal_service_rejections_reach_the_sale_in_spanish(string code, string expected)
    {
        var body = $$"""{"code":"{{code}}","description":"Receiver identification is required above the configured ARCA threshold.","type":1}""";
        var service = new FiscalizationService(
            new HttpClient(new StubHandler(HttpStatusCode.BadRequest, body)),
            Options.Create(new FiscalizationOptions { BaseUrl = "http://fiscal.local", ApiKey = "test-key" }),
            NullLogger<FiscalizationService>.Instance);

        var result = await service.RequestDocumentAsync(new FiscalDocumentRequest(
            "req-1", Guid.NewGuid(), FiscalRequestedDocumentType.Auto, null,
            new FiscalAmounts(10m, [new FiscalVatAmount(21m, 10m, 2.1m)], 0m, 12.1m), new DateOnly(2026, 9, 26)));

        result.Outcome.Should().Be(FiscalDocumentOutcome.Rejected);
        result.ErrorMessage.Should().Contain(expected);
    }

    [Theory]
    [InlineData("10069: Campo DocNro no puede ser igual al del emisor.", "mismo CUIT que quien factura")]
    [InlineData("10015: Factura A: el receptor debe ser responsable inscripto.", "ARCA: 10015: Factura A")]
    public async Task An_arca_rejection_keeps_its_reason(string failureReason, string expected)
    {
        // Antes el servicio no devolvia el motivo y EITI no lo leia: la venta quedaba "rechazada" sin explicacion.
        var body = $$"""{"documentId":"d7133a09-3ced-4b6e-b698-0bb38782a965","status":"rejected","type":"invoiceA","pointOfSale":1,"number":null,"authorizationCode":null,"validUntil":null,"qr":null,"isQueued":false,"isDuplicate":false,"failureReason":"{{failureReason}}"}""";
        var service = new FiscalizationService(
            new HttpClient(new StubHandler(HttpStatusCode.OK, body)),
            Options.Create(new FiscalizationOptions { BaseUrl = "http://fiscal.local", ApiKey = "test-key" }),
            NullLogger<FiscalizationService>.Instance);

        var result = await service.RequestDocumentAsync(new FiscalDocumentRequest(
            "req-1", Guid.NewGuid(), FiscalRequestedDocumentType.Auto, null,
            new FiscalAmounts(10m, [new FiscalVatAmount(21m, 10m, 2.1m)], 0m, 12.1m), new DateOnly(2026, 9, 29)));

        result.Outcome.Should().Be(FiscalDocumentOutcome.Rejected);
        result.ErrorMessage.Should().Contain(expected);
    }

    [Fact]
    public async Task A_sale_that_asks_for_an_invoice_is_not_created_when_the_branch_has_no_point_of_sale()
    {
        var branch = Branch.Create(_companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(_companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(branch);
        var sales = new Mock<ISaleRepository>();
        var invoicing = new Mock<ISaleInvoicingService>();
        invoicing.SetupGet(x => x.IsEnabled).Returns(true);

        var handler = new CreateSaleHandler(
            currentUser.Object, branches.Object, new Mock<ICustomerRepository>().Object, new Mock<IProductRepository>().Object,
            new Mock<IBranchProductStockRepository>().Object, new Mock<IStockMovementRepository>().Object, sales.Object,
            new Mock<ICashDrawerRepository>().Object, new Mock<ICashSessionRepository>().Object, new Mock<IAddressRepository>().Object,
            new Mock<IBankRepository>().Object, new Mock<IChequeRepository>().Object, invoicing.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreateSaleCommand(branch.Id.Value, null, 1, false, null,
                [new CreateSaleDetailItemRequest(product.Id.Value, 1)], [], [], RequestInvoicing: true),
            CancellationToken.None);

        result.Error.Code.Should().Be("Sales.Create.BranchWithoutPointOfSale");
        sales.Verify(x => x.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_sale_that_asks_for_an_invoice_is_not_created_when_the_customer_is_missing_the_cuit()
    {
        var branch = _branch;
        var product = Product.Create(_companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var customer = Customer(IvaCondition.ResponsableInscripto, taxId: null);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(branch);
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(x => x.GetByIdAsync(customer.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        var sales = new Mock<ISaleRepository>();
        var invoicing = new Mock<ISaleInvoicingService>();
        invoicing.SetupGet(x => x.IsEnabled).Returns(true);

        var handler = new CreateSaleHandler(
            currentUser.Object, branches.Object, customers.Object, new Mock<IProductRepository>().Object,
            new Mock<IBranchProductStockRepository>().Object, new Mock<IStockMovementRepository>().Object, sales.Object,
            new Mock<ICashDrawerRepository>().Object, new Mock<ICashSessionRepository>().Object, new Mock<IAddressRepository>().Object,
            new Mock<IBankRepository>().Object, new Mock<IChequeRepository>().Object, invoicing.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreateSaleCommand(branch.Id.Value, customer.Id.Value, 1, false, null,
                [new CreateSaleDetailItemRequest(product.Id.Value, 1)], [], [], RequestInvoicing: true),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.Create.InvoicingReceiverInvalid");
        result.Error.Description.Should().Contain("hay que cargar su CUIT");
        sales.Verify(x => x.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_counter_sale_asking_for_invoice_a_without_customer_is_not_created()
    {
        var branch = _branch;
        var product = Product.Create(_companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(branch);
        var sales = new Mock<ISaleRepository>();
        var invoicing = new Mock<ISaleInvoicingService>();
        invoicing.SetupGet(x => x.IsEnabled).Returns(true);

        var handler = new CreateSaleHandler(
            currentUser.Object, branches.Object, new Mock<ICustomerRepository>().Object, new Mock<IProductRepository>().Object,
            new Mock<IBranchProductStockRepository>().Object, new Mock<IStockMovementRepository>().Object, sales.Object,
            new Mock<ICashDrawerRepository>().Object, new Mock<ICashSessionRepository>().Object, new Mock<IAddressRepository>().Object,
            new Mock<IBankRepository>().Object, new Mock<IChequeRepository>().Object, invoicing.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreateSaleCommand(branch.Id.Value, null, 1, false, null,
                [new CreateSaleDetailItemRequest(product.Id.Value, 1)], [], [], RequestInvoicing: true, InvoiceLetter: InvoiceLetter.A),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.Create.InvoicingReceiverInvalid");
        result.Error.Description.Should().StartWith("Para hacer Factura A");
        sales.Verify(x => x.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Customer Customer(IvaCondition? condition, string? taxId) =>
        eiti.Domain.Customers.Customer.Create(_companyId, "Juan", "Perez", null, taxId: taxId, ivaCondition: condition);

    private Sale Sale(Customer customer) =>
        eiti.Domain.Sales.Sale.Create(_companyId, _branch.Id, customer.Id, false, SaleStatus.OnHold,
            [SaleDetail.Create(ProductId.New(), 1, 1210m)]);

    private SaleInvoicingService InvoicingService(
        Mock<IFiscalizationService> fiscal,
        Customer customer,
        IReadOnlyList<SaleFiscalDocument> existing,
        List<SaleFiscalDocument> added)
    {
        var documents = new Mock<ISaleFiscalDocumentRepository>();
        documents.Setup(x => x.ListBySaleAsync(It.IsAny<SaleId>(), _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        documents.Setup(x => x.AddAsync(It.IsAny<SaleFiscalDocument>(), It.IsAny<CancellationToken>()))
            .Callback<SaleFiscalDocument, CancellationToken>((d, _) => added.Add(d))
            .Returns(Task.CompletedTask);
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(x => x.GetByIdAsync(customer.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(_branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_branch);

        return new SaleInvoicingService(fiscal.Object, documents.Object, customers.Object,
            new Mock<ICompanyRepository>().Object, branches.Object);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
