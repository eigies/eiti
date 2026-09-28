using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Commands.CreateCcSale;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Application.Features.Sales.Common;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

/// <summary>
/// La venta de cuenta corriente factura igual que la de mostrador: con el tilde, con la letra
/// elegida y cuando la sucursal factura sola.
/// </summary>
public sealed class CreateCcSaleInvoicingTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _branch;
    private readonly Product _product;
    private readonly Mock<ISaleRepository> _sales = new();
    private readonly Mock<ISaleInvoicingService> _invoicing = new();

    public CreateCcSaleInvoicingTests()
    {
        _branch = Branch.Create(_companyId, "Sucursal Centro", "SC", "San Martin 123");
        _product = Product.Create(_companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        _invoicing.SetupGet(x => x.IsEnabled).Returns(true);
    }

    [Fact]
    public async Task Asking_for_an_invoice_for_a_registered_customer_without_cuit_does_not_create_the_sale()
    {
        var customer = Customer(IvaCondition.ResponsableInscripto, taxId: null);

        var result = await Handler(customer).Handle(Command(customer, requestInvoicing: true, InvoiceLetter.A), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.CreateCc.InvoicingReceiverInvalid");
        result.Error.Description.Should().Contain("hay que cargar su CUIT");
        _sales.Verify(x => x.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Asking_for_an_invoice_invoices_the_sale_and_returns_the_outcome()
    {
        var customer = Customer(IvaCondition.ResponsableInscripto, taxId: "30-71999999-5");
        _invoicing.Setup(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaleInvoicingOutcome(true, SaleInvoicingStatus.Invoiced));

        var result = await Handler(customer).Handle(Command(customer, requestInvoicing: true, InvoiceLetter.A), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Invoicing!.Status.Should().Be((int)SaleInvoicingStatus.Invoiced);
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_branch_that_invoices_automatically_invoices_cc_sales_too()
    {
        var customer = Customer(IvaCondition.ConsumidorFinal, taxId: null);
        _invoicing.Setup(x => x.IsAutomaticAsync(_companyId, _branch.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _invoicing.Setup(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaleInvoicingOutcome(true, SaleInvoicingStatus.Invoiced));

        var result = await Handler(customer).Handle(Command(customer, requestInvoicing: false, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Without_invoicing_the_sale_is_not_invoiced()
    {
        var customer = Customer(IvaCondition.ConsumidorFinal, taxId: null);

        var result = await Handler(customer).Handle(Command(customer, requestInvoicing: false, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Invoicing.Should().BeNull();
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private CreateCcSaleCommand Command(Customer customer, bool requestInvoicing, InvoiceLetter? letter) =>
        new(_branch.Id.Value, customer.Id.Value, [new CreateSaleDetailItemRequest(_product.Id.Value, 1)],
            RequestInvoicing: requestInvoicing, InvoiceLetter: letter);

    private Customer Customer(IvaCondition condition, string? taxId) =>
        eiti.Domain.Customers.Customer.Create(_companyId, "Juan", "Perez", null, taxId: taxId, ivaCondition: condition);

    private CreateCcSaleHandler Handler(Customer customer)
    {
        var stock = BranchProductStock.Create(_companyId, _branch.Id, _product.Id);
        stock.ApplyManualEntry(10);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(_branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_branch);
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(x => x.GetByIdAsync(customer.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        var products = new Mock<IProductRepository>();
        products.Setup(x => x.GetByIdAsync(_product.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_product);
        var stocks = new Mock<IBranchProductStockRepository>();
        stocks.Setup(x => x.GetOrCreateAsync(_branch.Id, _product.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(stock);

        return new CreateCcSaleHandler(currentUser.Object, branches.Object, customers.Object, products.Object, stocks.Object,
            new Mock<IStockMovementRepository>().Object, _sales.Object, _invoicing.Object, new Mock<IUnitOfWork>().Object);
    }
}
