using System.Text.Json;
using eiti.Api.Controllers;
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Sales.Commands.CreateCcSale;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Application.Features.Sales.Commands.UpdateSale;
using eiti.Application.Features.Sales.Common;
using eiti.Application.Features.Sales.Queries.GetSaleById;
using eiti.Application.Features.Sales.Queries.ListSales;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Users;
using FluentAssertions;
using MediatR;
using Moq;

namespace eiti.Tests;

// El vendedor aplica un descuento a la venta minorista dentro del máximo que fijó el dueño; las CC no tienen tope.
// Cada descuento general queda trazado: quién, cuándo y por qué.
public sealed class RetailDiscountSaleTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly Company _company = Company.Create(CompanyName.Create("Baterías Soler"), CompanyDomain.Create("soler.com"));
    private readonly Branch _branch;
    private readonly Product _product;
    private readonly BranchProductStock _stock;
    private readonly UserId _seller = UserId.New();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ISaleRepository> _sales = new();
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private Sale? _added;

    public RetailDiscountSaleTests()
    {
        _branch = Branch.Create(_company.Id, "Sucursal Centro", "SC", "San Martin 123");
        _product = Product.Create(_company.Id, "BAT-001", "BAT-001", "Moura", "Bateria 12x65", null, 100000m, 70000m, null);
        _stock = BranchProductStock.Create(_company.Id, _branch.Id, _product.Id);
        _stock.ApplyManualEntry(10);
        _companies.Setup(r => r.GetByIdAsync(_company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_company);
        _sales.Setup(r => r.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Callback<Sale, CancellationToken>((sale, _) => _added = sale)
            .Returns(Task.CompletedTask);
    }

    // --- Alta ---

    [Fact]
    public async Task Create_RejectsAGeneralDiscountOverTheLimit_AndDoesNotCreateTheSale()
    {
        _company.SetMaxRetailDiscountPercent(10m);

        var result = await CreateHandler().Handle(CreateCommand(generalDiscount: 15m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Sales.Discount.ExceedsLimit");
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Description.Should().Be("El descuento supera el máximo de 10% que fijó el dueño.");
        _sales.Verify(r => r.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _stock.ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Create_WithoutALimitConfigured_RejectsAnyDiscount()
    {
        var result = await CreateHandler().Handle(CreateCommand(generalDiscount: 5m), CancellationToken.None);

        result.Error!.Code.Should().Be("Sales.Discount.ExceedsLimit");
        result.Error.Description.Should().Be("El descuento supera el máximo de 0% que fijó el dueño.");
    }

    [Fact]
    public async Task Create_RejectsALineDiscountOverTheLimit()
    {
        _company.SetMaxRetailDiscountPercent(10m);

        var result = await CreateHandler().Handle(CreateCommand(lineDiscount: 12m), CancellationToken.None);

        result.Error!.Code.Should().Be("Sales.Discount.ExceedsLimit");
    }

    [Fact]
    public async Task Create_TheMessageShowsADecimalLimitTheArgentineWay()
    {
        _company.SetMaxRetailDiscountPercent(12.5m);

        var result = await CreateHandler().Handle(CreateCommand(generalDiscount: 13m), CancellationToken.None);

        result.Error!.Description.Should().Be("El descuento supera el máximo de 12,5% que fijó el dueño.");
    }

    [Fact]
    public async Task Create_WithinTheLimit_SavesTheDiscountWithWhoWhenAndWhy()
    {
        _company.SetMaxRetailDiscountPercent(10m);

        var result = await CreateHandler().Handle(
            CreateCommand(generalDiscount: 10m, reason: "  Cliente de años  "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error!.Description : string.Empty);
        _added!.GeneralDiscountPercent.Should().Be(10m);
        _added.TotalAmount.Should().Be(90000m);
        _added.GeneralDiscountByUserId.Should().Be(_seller.Value);
        _added.GeneralDiscountAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        _added.GeneralDiscountReason.Should().Be("Cliente de años");
    }

    [Fact]
    public async Task Create_WithoutDiscount_DoesNotEvenLookAtTheLimit()
    {
        var result = await CreateHandler().Handle(CreateCommand(reason: "ignorado"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _added!.GeneralDiscountByUserId.Should().BeNull();
        _added.GeneralDiscountReason.Should().BeNull();
        _companies.Verify(r => r.GetByIdAsync(It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CreateValidator_RejectsAReasonOverTheMaximum()
    {
        var command = CreateCommand(generalDiscount: 5m, reason: new string('x', Sale.GeneralDiscountReasonMaxLength + 1));

        new CreateSaleValidator().Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateSaleCommand.GeneralDiscountReason));
    }

    // --- Edición ---

    [Fact]
    public async Task Update_KeepsADiscountThatWasAlreadyThere_EvenIfTheLimitWentDown()
    {
        var firstSeller = Guid.NewGuid();
        var sale = StoredSale(generalDiscount: 15m, by: firstSeller, reason: "Cliente de años");
        _company.SetMaxRetailDiscountPercent(10m);

        var result = await UpdateHandler(sale).Handle(UpdateCommand(sale, generalDiscount: 15m, reason: "Cliente de años"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error!.Description : string.Empty);
        sale.GeneralDiscountPercent.Should().Be(15m);
        sale.GeneralDiscountByUserId.Should().Be(firstSeller);
        sale.GeneralDiscountReason.Should().Be("Cliente de años");
    }

    [Fact]
    public async Task Update_RejectsChangingTheDiscountToAValueOverTheLimit()
    {
        var sale = StoredSale(generalDiscount: 15m, by: Guid.NewGuid(), reason: null);
        _company.SetMaxRetailDiscountPercent(10m);

        var result = await UpdateHandler(sale).Handle(UpdateCommand(sale, generalDiscount: 12m), CancellationToken.None);

        result.Error!.Code.Should().Be("Sales.Discount.ExceedsLimit");
        sale.GeneralDiscountPercent.Should().Be(15m);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ChangingTheDiscount_RecordsTheNewAuthor()
    {
        var sale = StoredSale(generalDiscount: 15m, by: Guid.NewGuid(), reason: "Viejo");
        _company.SetMaxRetailDiscountPercent(10m);

        var result = await UpdateHandler(sale).Handle(UpdateCommand(sale, generalDiscount: 5m, reason: "Pagó en efectivo"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error!.Description : string.Empty);
        sale.GeneralDiscountByUserId.Should().Be(_seller.Value);
        sale.GeneralDiscountReason.Should().Be("Pagó en efectivo");
    }

    [Fact]
    public async Task Update_RemovingTheDiscount_ClearsTheTrace()
    {
        var sale = StoredSale(generalDiscount: 15m, by: Guid.NewGuid(), reason: "Viejo");

        var result = await UpdateHandler(sale).Handle(UpdateCommand(sale, generalDiscount: 0m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error!.Description : string.Empty);
        sale.GeneralDiscountByUserId.Should().BeNull();
        sale.GeneralDiscountAt.Should().BeNull();
        sale.GeneralDiscountReason.Should().BeNull();
    }

    [Fact]
    public async Task Update_KeepsALineDiscountThatWasAlreadyThere_ButRejectsChangingIt()
    {
        var sale = StoredSale(generalDiscount: 0m, by: null, reason: null, lineDiscount: 15m);
        _company.SetMaxRetailDiscountPercent(10m);

        var unchanged = await UpdateHandler(sale).Handle(UpdateCommand(sale, lineDiscount: 15m), CancellationToken.None);
        unchanged.IsSuccess.Should().BeTrue(unchanged.IsFailure ? unchanged.Error!.Description : string.Empty);

        var changed = await UpdateHandler(sale).Handle(UpdateCommand(sale, lineDiscount: 12m), CancellationToken.None);
        changed.Error!.Code.Should().Be("Sales.Discount.ExceedsLimit");
    }

    [Fact]
    public async Task Update_CuentaCorrienteHasNoLimit()
    {
        var customer = Customer.Create(_company.Id, "Juan", "Perez", null);
        _customers.Setup(r => r.GetByIdAsync(customer.Id, _company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        var sale = Sale.CreateCc(_company.Id, _branch.Id, customer.Id, [SaleDetail.Create(_product.Id, 1, _product.Price)]);
        _stock.Reserve(1);

        var result = await UpdateHandler(sale).Handle(UpdateCommand(sale, generalDiscount: 30m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error!.Description : string.Empty);
        sale.GeneralDiscountPercent.Should().Be(30m);
        sale.GeneralDiscountByUserId.Should().Be(_seller.Value);
    }

    [Fact]
    public void UpdateValidator_RejectsAReasonOverTheMaximum()
    {
        var sale = StoredSale(0m, null, null);
        var command = UpdateCommand(sale, reason: new string('x', Sale.GeneralDiscountReasonMaxLength + 1));

        new UpdateSaleValidator().Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(UpdateSaleCommand.GeneralDiscountReason));
    }

    // --- Cuenta corriente: sin tope, pero con traza ---

    [Fact]
    public async Task CreateCc_IgnoresTheLimitAndRecordsWhoAppliedTheDiscount()
    {
        var customer = Customer.Create(_company.Id, "Juan", "Perez", null);
        var currentUser = CurrentUser();
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.GetByIdAsync(customer.Id, _company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.GetByIdAsync(_product.Id, _company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_product);
        var handler = new CreateCcSaleHandler(
            currentUser.Object, Branches().Object, customers.Object, products.Object, Stocks().Object,
            new Mock<IStockMovementRepository>().Object, _sales.Object, new Mock<ISaleInvoicingService>().Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new CreateCcSaleCommand(_branch.Id.Value, customer.Id.Value, [new CreateSaleDetailItemRequest(_product.Id.Value, 1)],
                GeneralDiscountPercent: 30m),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error!.Description : string.Empty);
        _added!.GeneralDiscountByUserId.Should().Be(_seller.Value);
        _added.GeneralDiscountAt.Should().NotBeNull();
    }

    // --- Por HTTP (lección 2026-10-23: un campo no está terminado hasta que llega por la API) ---

    [Fact]
    public async Task Http_TheReasonTravelsFromTheJsonToTheCreateCommand()
    {
        var command = JsonSerializer.Deserialize<CreateSaleCommand>(
            """
            { "branchId": "8d4f0b8e-7a6b-4d0c-9a51-0d6c6f3d2a11", "customerId": null, "idSaleStatus": 1, "hasDelivery": false,
              "cashDrawerId": null, "details": [], "payments": [], "tradeIns": [],
              "generalDiscountPercent": 10, "generalDiscountReason": "Cliente de años" }
            """,
            Web)!;
        CreateSaleCommand? sent = null;
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<CreateSaleCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<CreateSaleResponse>>, CancellationToken>((c, _) => sent = (CreateSaleCommand)c)
            .ReturnsAsync(Result<CreateSaleResponse>.Failure(Error.Validation("Test.Stop", "solo interesa el comando")));

        await new SalesController(sender.Object).CreateSale(command, CancellationToken.None);

        sent!.GeneralDiscountPercent.Should().Be(10m);
        sent.GeneralDiscountReason.Should().Be("Cliente de años");
    }

    [Fact]
    public async Task Http_TheReasonTravelsFromTheJsonToTheUpdateCommand()
    {
        var command = JsonSerializer.Deserialize<UpdateSaleCommand>(
            """
            { "customerId": null, "idSaleStatus": 2, "hasDelivery": false, "cashDrawerId": null,
              "details": [], "payments": [], "tradeIns": [],
              "generalDiscountPercent": 10, "generalDiscountReason": "Cliente de años" }
            """,
            Web)!;
        UpdateSaleCommand? sent = null;
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<UpdateSaleCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<UpdateSaleResponse>>, CancellationToken>((c, _) => sent = (UpdateSaleCommand)c)
            .ReturnsAsync(Result<UpdateSaleResponse>.Failure(Error.Validation("Test.Stop", "solo interesa el comando")));

        await new SalesController(sender.Object).UpdateSale(Guid.NewGuid(), command, CancellationToken.None);

        sent!.GeneralDiscountPercent.Should().Be(10m);
        sent.GeneralDiscountReason.Should().Be("Cliente de años");
    }

    // --- Lecturas ---

    [Fact]
    public async Task ListSales_ReturnsTheDiscountAndItsReason()
    {
        var sale = StoredSale(generalDiscount: 10m, by: Guid.NewGuid(), reason: "Cliente de años");
        sale.SetSourceChannel(null);
        var currentUser = CurrentUser();
        currentUser.SetupGet(s => s.CanViewAllBranches).Returns(true);
        _sales.Setup(r => r.ListByCodeAsync(_company.Id, "SC-001", It.IsAny<CancellationToken>())).ReturnsAsync([sale]);
        var transport = new Mock<ISaleTransportAssignmentRepository>();
        transport.Setup(r => r.ListBySaleIdsAsync(It.IsAny<IReadOnlyList<SaleId>>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var fiscal = new Mock<ISaleFiscalDocumentRepository>();
        fiscal.Setup(r => r.ListBySaleIdsAsync(It.IsAny<IReadOnlyCollection<SaleId>>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.ListByIdsAsync(It.IsAny<CompanyId>(), It.IsAny<IEnumerable<CustomerId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = new ListSalesHandler(
            currentUser.Object, _sales.Object, products.Object, customers.Object, transport.Object,
            new Mock<IEmployeeRepository>().Object, new Mock<IVehicleRepository>().Object, new Mock<IAddressRepository>().Object, fiscal.Object);

        var result = await handler.Handle(new ListSalesQuery(null, null, null, Code: "SC-001"), CancellationToken.None);

        var item = result.Value.Should().ContainSingle().Subject;
        item.GeneralDiscountPercent.Should().Be(10m);
        item.GeneralDiscountReason.Should().Be("Cliente de años");
    }

    [Fact]
    public async Task GetSaleById_ReturnsTheDiscountAndItsReason()
    {
        var sale = StoredSale(generalDiscount: 10m, by: Guid.NewGuid(), reason: "Cliente de años");
        _sales.Setup(r => r.GetByIdWithCcPaymentsAsync(sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sale);
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), _company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_product]);
        var fiscal = new Mock<ISaleFiscalDocumentRepository>();
        fiscal.Setup(r => r.ListBySaleIdsAsync(It.IsAny<IReadOnlyCollection<SaleId>>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = new GetSaleByIdHandler(
            CurrentUser().Object, _sales.Object, _customers.Object, products.Object, fiscal.Object, new Mock<IAddressRepository>().Object);

        var result = await handler.Handle(new GetSaleByIdQuery(sale.Id.Value), CancellationToken.None);

        result.Value.GeneralDiscountPercent.Should().Be(10m);
        result.Value.GeneralDiscountReason.Should().Be("Cliente de años");
    }

    // --- Armado ---

    private CreateSaleCommand CreateCommand(decimal generalDiscount = 0m, decimal lineDiscount = 0m, string? reason = null) =>
        new(_branch.Id.Value, null, (int)SaleStatus.OnHold, false, null,
            [new CreateSaleDetailItemRequest(_product.Id.Value, 1, DiscountPercent: lineDiscount)], [], [],
            GeneralDiscountPercent: generalDiscount, GeneralDiscountReason: reason);

    private UpdateSaleCommand UpdateCommand(Sale sale, decimal generalDiscount = 0m, decimal lineDiscount = 0m, string? reason = null) =>
        new(sale.Id.Value, sale.CustomerId?.Value, (int)SaleStatus.OnHold, false, null,
            [new UpdateSaleDetailItemRequest(_product.Id.Value, 1, DiscountPercent: lineDiscount)], [], [],
            GeneralDiscountPercent: generalDiscount, GeneralDiscountReason: reason);

    // Venta en espera ya guardada (con el stock reservado), como la deja el alta.
    private Sale StoredSale(decimal generalDiscount, Guid? by, string? reason, decimal lineDiscount = 0m)
    {
        var sale = Sale.Create(_company.Id, _branch.Id, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(_product.Id, 1, _product.Price, lineDiscount)], generalDiscountPercent: generalDiscount, code: "SC-001");
        sale.TrackGeneralDiscount(0m, by, reason);
        _stock.Reserve(1);
        return sale;
    }

    private Mock<ICurrentUserService> CurrentUser()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUser.SetupGet(s => s.CompanyId).Returns(_company.Id);
        currentUser.SetupGet(s => s.UserId).Returns(_seller);
        return currentUser;
    }

    private Mock<IBranchRepository> Branches()
    {
        var branches = new Mock<IBranchRepository>();
        branches.Setup(r => r.GetByIdAsync(_branch.Id, _company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_branch);
        return branches;
    }

    private Mock<IBranchProductStockRepository> Stocks()
    {
        var stocks = new Mock<IBranchProductStockRepository>();
        stocks.Setup(r => r.GetOrCreateAsync(_branch.Id, _product.Id, _company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_stock);
        return stocks;
    }

    private CreateSaleHandler CreateHandler()
    {
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), _company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_product]);

        return new CreateSaleHandler(
            CurrentUser().Object, Branches().Object, new Mock<ICustomerRepository>().Object, products.Object, Stocks().Object,
            new Mock<IStockMovementRepository>().Object, _sales.Object, new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object, new Mock<IAddressRepository>().Object, new Mock<IBankRepository>().Object,
            new Mock<IChequeRepository>().Object, new Mock<ISaleInvoicingService>().Object,
            new RetailDiscountPolicy(_companies.Object), _unitOfWork.Object);
    }

    private UpdateSaleHandler UpdateHandler(Sale sale)
    {
        _sales.Setup(r => r.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sale);
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.GetByIdAsync(_product.Id, _company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_product);
        var fiscal = new Mock<ISaleFiscalDocumentRepository>();
        fiscal.Setup(r => r.ListBySaleAsync(It.IsAny<SaleId>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        return new UpdateSaleHandler(
            CurrentUser().Object, _sales.Object, _customers.Object, products.Object, Stocks().Object,
            new Mock<IStockMovementRepository>().Object, new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object, new Mock<ISaleTransportAssignmentRepository>().Object,
            new Mock<IAddressRepository>().Object, new Mock<IBankRepository>().Object, fiscal.Object,
            new Mock<ICustomerCreditNoteRepository>().Object, new RetailDiscountPolicy(_companies.Object), _unitOfWork.Object);
    }
}
