using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Commands.InvoiceSale;
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
/// "Facturar" después del alta con la letra elegida: se valida contra el cliente antes de pedir nada
/// al servicio, igual que en el alta.
/// </summary>
public sealed class InvoiceSaleLetterTests
{
    private const string ValidCuit = "30-71999999-5";

    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _branch;
    private readonly Mock<ISaleInvoicingService> _invoicing = new();
    private readonly List<SaleFiscalDocument> _documents = [];

    public InvoiceSaleLetterTests()
    {
        _branch = Branch.Create(_companyId, "Sucursal Centro", "SC", "San Martin 123");
        _branch.AssignFiscalPointOfSale(FiscalPointOfSale.Create(_companyId, 3));
        _invoicing.SetupGet(x => x.IsEnabled).Returns(true);
        _invoicing.Setup(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaleInvoicingOutcome(true, SaleInvoicingStatus.Invoiced));
    }

    [Fact]
    public async Task Letter_a_for_a_registered_customer_without_cuit_is_refused_without_calling_the_service()
    {
        var customer = Customer(IvaCondition.ResponsableInscripto, taxId: null);

        var result = await Invoice(customer, InvoiceLetter.A);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.Invoice.ReceiverInvalid");
        result.Error.Description.Should().Contain("hay que cargar su CUIT");
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Letter_a_for_a_counter_sale_without_customer_is_refused()
    {
        var result = await Invoice(null, InvoiceLetter.A);

        result.Error.Code.Should().Be("Sales.Invoice.ReceiverInvalid");
        result.Error.Description.Should().StartWith("Para hacer Factura A");
    }

    [Fact]
    public async Task Letter_b_for_a_registered_customer_is_refused()
    {
        var result = await Invoice(Customer(IvaCondition.ResponsableInscripto, ValidCuit), InvoiceLetter.B);

        result.Error.Description.Should().Contain("le corresponde Factura A");
    }

    [Fact]
    public async Task A_matching_letter_invoices()
    {
        var result = await Invoice(Customer(IvaCondition.ResponsableInscripto, ValidCuit), InvoiceLetter.A);

        result.IsSuccess.Should().BeTrue();
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_branch_without_point_of_sale_is_refused_without_calling_the_service()
    {
        _branch.ClearFiscalPointOfSale();

        var result = await Invoice(Customer(IvaCondition.ResponsableInscripto, ValidCuit), null);

        result.Error.Code.Should().Be("Sales.Invoice.BranchWithoutPointOfSale");
        result.Error.Description.Should().StartWith("La sucursal Sucursal Centro no tiene punto de venta");
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_attempt_in_flight_is_resent_even_if_the_branch_lost_its_point_of_sale()
    {
        var customer = Customer(IvaCondition.ConsumidorFinal, taxId: null);
        var sale = Sale(customer);
        var inFlight = SaleFiscalDocument.CreateInvoice(_companyId, sale.Id, 1);
        inFlight.MarkUnconfirmed("timeout");
        _documents.Add(inFlight);
        _branch.ClearFiscalPointOfSale();

        var result = await Invoice(customer, null, sale);

        result.IsSuccess.Should().BeTrue();
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Without_a_letter_it_behaves_as_before()
    {
        var result = await Invoice(Customer(IvaCondition.ResponsableInscripto, taxId: null), null);

        result.IsSuccess.Should().BeTrue();
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_attempt_in_flight_is_resent_even_if_the_letter_no_longer_matches()
    {
        // El intento en trámite pudo haberse emitido: frenarlo dejaría la venta trabada.
        var customer = Customer(IvaCondition.ConsumidorFinal, taxId: null);
        var sale = Sale(customer);
        var inFlight = SaleFiscalDocument.CreateInvoice(_companyId, sale.Id, 1);
        inFlight.MarkUnconfirmed("timeout");
        _documents.Add(inFlight);

        var result = await Invoice(customer, InvoiceLetter.A, sale);

        result.IsSuccess.Should().BeTrue();
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task<eiti.Application.Common.Result<InvoiceSaleResponse>> Invoice(Customer? customer, InvoiceLetter? letter, Sale? sale = null)
    {
        sale ??= Sale(customer);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        var sales = new Mock<ISaleRepository>();
        sales.Setup(x => x.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sale);
        var fiscalDocuments = new Mock<ISaleFiscalDocumentRepository>();
        fiscalDocuments.Setup(x => x.ListBySaleAsync(sale.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(() => _documents);
        var customers = new Mock<ICustomerRepository>();
        if (customer is not null)
        {
            customers.Setup(x => x.GetByIdAsync(customer.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        }

        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(_branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_branch);

        var handler = new InvoiceSaleHandler(currentUser.Object, sales.Object, fiscalDocuments.Object, _invoicing.Object,
            customers.Object, branches.Object, new Mock<IUnitOfWork>().Object);

        return await handler.Handle(new InvoiceSaleCommand(sale.Id.Value, letter), CancellationToken.None);
    }

    private Customer Customer(IvaCondition condition, string? taxId) =>
        eiti.Domain.Customers.Customer.Create(_companyId, "Juan", "Perez", null, taxId: taxId, ivaCondition: condition);

    private Sale Sale(Customer? customer) =>
        eiti.Domain.Sales.Sale.Create(_companyId, _branch.Id, customer?.Id, false, SaleStatus.OnHold,
            [SaleDetail.Create(ProductId.New(), 1, 1210m)]);
}
