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
/// El mismo pedido de alta puede llegar dos veces: Chrome reenvía un POST por su cuenta cuando la conexión
/// se traba, y el servidor recibe los dos (MAT-4194 y MAT-4195, 2026-11-19, las dos facturadas en ARCA).
/// Con el id de intento que manda el front, el segundo pedido devuelve la venta que ya existe en vez de
/// crear otra.
/// </summary>
public sealed class SaleIdempotencyTests
{
    private static readonly Guid RequestId = Guid.Parse("6f1c2a7e-3b4d-4e5f-8a9b-0c1d2e3f4a5b");

    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _branch;
    private readonly Product _product;
    private readonly Customer _customer;
    private readonly Mock<ISaleRepository> _sales = new();
    private readonly Mock<ISaleInvoicingService> _invoicing = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IBranchProductStockRepository> _stocks = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<ICustomerRepository> _customers = new();

    public SaleIdempotencyTests()
    {
        _branch = Branch.Create(_companyId, "Sucursal Centro", "SC", "San Martin 123");
        _product = Product.Create(_companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        _customer = Customer.Create(_companyId, "Juan", "Perez", null);

        var stock = BranchProductStock.Create(_companyId, _branch.Id, _product.Id);
        stock.ApplyManualEntry(10);
        _stocks.Setup(x => x.GetOrCreateAsync(_branch.Id, _product.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(stock);
        _products.Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { _product });
        _products.Setup(x => x.GetByIdAsync(_product.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_product);
        _customers.Setup(x => x.GetByIdAsync(_customer.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_customer);
    }

    [Fact]
    public void A_sale_keeps_the_request_id_it_was_created_with()
    {
        var sale = CounterSale();

        sale.AssignClientRequestId(RequestId);

        sale.ClientRequestId.Should().Be(RequestId);
    }

    [Fact]
    public void The_request_id_of_a_sale_cannot_be_replaced()
    {
        var sale = CounterSale();
        sale.AssignClientRequestId(RequestId);

        var replace = () => sale.AssignClientRequestId(Guid.NewGuid());

        replace.Should().Throw<InvalidOperationException>();
        sale.ClientRequestId.Should().Be(RequestId);
    }

    [Fact]
    public void An_empty_request_id_is_not_a_request_id()
    {
        var assign = () => CounterSale().AssignClientRequestId(Guid.Empty);

        assign.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Both_sale_requests_reject_an_empty_request_id()
    {
        var counter = new CreateSaleValidator().Validate(CounterCommand() with { ClientRequestId = Guid.Empty });
        var cc = new CreateCcSaleValidator().Validate(CcCommand() with { ClientRequestId = Guid.Empty });

        counter.Errors.Should().Contain(error => error.PropertyName == nameof(CreateSaleCommand.ClientRequestId));
        cc.Errors.Should().Contain(error => error.PropertyName == nameof(CreateCcSaleCommand.ClientRequestId));
    }

    [Fact]
    public async Task Repeating_a_counter_sale_request_returns_the_sale_it_already_created()
    {
        var existing = CounterSale();
        existing.AssignClientRequestId(RequestId);
        _sales.Setup(x => x.GetByClientRequestIdAsync(_companyId, RequestId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await CounterHandler().Handle(CounterCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id.Value);
        result.Value.Code.Should().Be("SC-001");
        result.Value.Details.Should().ContainSingle().Which.ProductName.Should().Be("Bateria nueva");
        _sales.Verify(x => x.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
        _stocks.Verify(x => x.GetOrCreateAsync(It.IsAny<BranchId>(), It.IsAny<ProductId>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_repeated_request_shows_the_invoice_the_first_one_got()
    {
        var existing = CounterSale();
        existing.AssignClientRequestId(RequestId);
        _sales.Setup(x => x.GetByClientRequestIdAsync(_companyId, RequestId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _invoicing.SetupGet(x => x.IsEnabled).Returns(true);
        _invoicing.Setup(x => x.GetInvoiceOutcomeAsync(existing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaleInvoicingOutcome(false, SaleInvoicingStatus.InProgress));

        var result = await CounterHandler().Handle(CounterCommand() with { RequestInvoicing = true }, CancellationToken.None);

        result.Value.Invoicing.Should().NotBeNull();
        result.Value.Invoicing!.Status.Should().Be((int)SaleInvoicingStatus.InProgress);
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_new_counter_sale_keeps_the_request_id()
    {
        Sale? persisted = null;
        _sales.Setup(x => x.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Callback<Sale, CancellationToken>((sale, _) => persisted = sale);

        var result = await CounterHandler().Handle(CounterCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted!.ClientRequestId.Should().Be(RequestId);
    }

    [Fact]
    public async Task Repeating_a_cc_sale_request_returns_the_sale_it_already_created()
    {
        var existing = Sale.CreateCc(_companyId, _branch.Id, _customer.Id, [SaleDetail.Create(_product.Id, 1, 100m, 0m, 70m)], code: "SC-002");
        existing.AssignClientRequestId(RequestId);
        _sales.Setup(x => x.GetByClientRequestIdAsync(_companyId, RequestId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await CcHandler().Handle(CcCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id.Value);
        result.Value.Code.Should().Be("SC-002");
        result.Value.CustomerFullName.Should().Be(_customer.FullName);
        _sales.Verify(x => x.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
        _stocks.Verify(x => x.GetOrCreateAsync(It.IsAny<BranchId>(), It.IsAny<ProductId>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_new_cc_sale_keeps_the_request_id()
    {
        Sale? persisted = null;
        _sales.Setup(x => x.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Callback<Sale, CancellationToken>((sale, _) => persisted = sale);

        var result = await CcHandler().Handle(CcCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted!.ClientRequestId.Should().Be(RequestId);
    }

    [Fact]
    public async Task An_abandoned_request_still_finishes_invoicing_the_sale_it_saved()
    {
        // MAT-4194: el navegador cortó el pedido mientras la factura estaba en ARCA. La venta ya estaba guardada,
        // pero el corte cancelaba el pedido de factura y EITI nunca registraba el comprobante que ARCA sí emitió.
        _branch.AssignFiscalPointOfSale(FiscalPointOfSale.Create(_companyId, 10));
        _invoicing.SetupGet(x => x.IsEnabled).Returns(true);
        CancellationToken invoiceToken = default;
        _invoicing.Setup(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Callback<Sale, CancellationToken>((_, token) => invoiceToken = token)
            .ReturnsAsync(new SaleInvoicingOutcome(true, SaleInvoicingStatus.Invoiced));
        var abandoned = new CancellationTokenSource();
        abandoned.Cancel();

        var result = await CounterHandler().Handle(CounterCommand() with { RequestInvoicing = true }, abandoned.Token);

        result.IsSuccess.Should().BeTrue();
        _invoicing.Verify(x => x.InvoiceAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Once);
        invoiceToken.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task Without_a_request_id_a_sale_is_created_as_before()
    {
        Sale? persisted = null;
        _sales.Setup(x => x.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Callback<Sale, CancellationToken>((sale, _) => persisted = sale);

        var result = await CounterHandler().Handle(CounterCommand() with { ClientRequestId = null }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted!.ClientRequestId.Should().BeNull();
        _sales.Verify(x => x.GetByClientRequestIdAsync(It.IsAny<CompanyId>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Sale CounterSale() =>
        Sale.Create(_companyId, _branch.Id, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(_product.Id, 1, 100m, 0m, 70m)], code: "SC-001");

    private CreateSaleCommand CounterCommand() =>
        new(_branch.Id.Value, null, (int)SaleStatus.OnHold, false, null,
            [new CreateSaleDetailItemRequest(_product.Id.Value, 1)], [], [], ClientRequestId: RequestId);

    private CreateCcSaleCommand CcCommand() =>
        new(_branch.Id.Value, _customer.Id.Value, [new CreateSaleDetailItemRequest(_product.Id.Value, 1)],
            ClientRequestId: RequestId);

    private ICurrentUserService CurrentUser()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        return currentUser.Object;
    }

    private IBranchRepository Branches()
    {
        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(_branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_branch);
        return branches.Object;
    }

    private CreateSaleHandler CounterHandler() =>
        new(CurrentUser(), Branches(), _customers.Object, _products.Object, _stocks.Object,
            new Mock<IStockMovementRepository>().Object, _sales.Object, new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object, new Mock<IAddressRepository>().Object, new Mock<IBankRepository>().Object,
            new Mock<IChequeRepository>().Object, _invoicing.Object, new RetailDiscountPolicy(new Mock<ICompanyRepository>().Object),
            _unitOfWork.Object);

    private CreateCcSaleHandler CcHandler() =>
        new(CurrentUser(), Branches(), _customers.Object, _products.Object, _stocks.Object,
            new Mock<IStockMovementRepository>().Object, _sales.Object, _invoicing.Object, _unitOfWork.Object);
}
