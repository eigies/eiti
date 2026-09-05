using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Customers.Commands.CancelCustomerCreditNote;
using eiti.Application.Features.Customers.Commands.CreateCustomerCreditNote;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Common;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CustomerCreditNoteHandlersTests
{
    // Contexto compartido por los tests: cliente, sucursal, sesión de caja abierta y los mocks
    // que CashSessionResolver necesita. Molde tomado de AddCustomerPaymentHandlerTests.
    private sealed class Fixture
    {
        public CompanyId CompanyId { get; } = CompanyId.New();
        public UserId UserId { get; } = UserId.New();
        public Branch Branch { get; }
        public CashSession Session { get; }
        public Customer Customer { get; }

        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<ICustomerRepository> Customers { get; } = new();
        public Mock<ICustomerCreditNoteRepository> Notes { get; } = new();
        public Mock<ISaleRepository> Sales { get; } = new();
        public Mock<ICashDrawerRepository> Drawers { get; } = new();
        public Mock<ICashSessionRepository> Sessions { get; } = new();
        public Mock<IBranchProductStockRepository> Stock { get; } = new();
        public Mock<IStockMovementRepository> StockMovements { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public List<CustomerCreditNote> Added { get; } = [];

        public Fixture(bool withOpenSession = true)
        {
            Branch = Branch.Create(CompanyId, "Sucursal Centro", "SC", "San Martin 123");
            var drawer = CashDrawer.Create(CompanyId, Branch.Id, "Caja 1");
            Session = CashSession.Open(CompanyId, Branch.Id, drawer.Id, UserId, 0m, null);
            Customer = Customer.Create(CompanyId, "Juan", "Perez", null);

            CurrentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
            CurrentUser.SetupGet(s => s.CompanyId).Returns(CompanyId);
            CurrentUser.SetupGet(s => s.UserId).Returns(UserId);

            Customers
                .Setup(r => r.GetByIdAsync(Customer.Id, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Customer);

            Drawers
                .Setup(r => r.GetByAssignedUserAsync(UserId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(drawer);

            Sessions
                .Setup(r => r.GetOpenByDrawerAsync(drawer.Id, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(withOpenSession ? Session : null);

            Sales
                .Setup(r => r.ListPendingCcSalesByCustomerAsync(CompanyId, Customer.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Sale>());

            Sales
                .Setup(r => r.ListByCreditNoteIdAsync(CompanyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Sale>());

            Notes
                .Setup(r => r.CountByBranchAsync(CompanyId.Value, Branch.Id.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            Notes
                .Setup(r => r.AddAsync(It.IsAny<CustomerCreditNote>(), It.IsAny<CancellationToken>()))
                .Callback<CustomerCreditNote, CancellationToken>((n, _) => Added.Add(n))
                .Returns(Task.CompletedTask);

            Stock
                .Setup(r => r.GetOrCreateAsync(
                    It.IsAny<BranchId>(), It.IsAny<ProductId>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BranchId b, ProductId p, CompanyId c, CancellationToken _) =>
                {
                    // Una venta CC ya reservó su stock; sin la reserva, ConfirmSaleOut del
                    // handler lanzaría y el fixture no reflejaría la realidad.
                    var stock = BranchProductStock.Create(c, b, p);
                    stock.ApplyManualEntry(100);
                    stock.Reserve(10);
                    return stock;
                });
        }

        public void WithPendingSales(params Sale[] sales)
        {
            Sales
                .Setup(r => r.ListPendingCcSalesByCustomerAsync(CompanyId, Customer.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sales.ToList());
        }

        public Sale CcSale(decimal total)
        {
            return Sale.CreateCc(
                CompanyId, Branch.Id, Customer.Id,
                [SaleDetail.Create(ProductId.New(), 1, total)]);
        }

        public CreateCustomerCreditNoteHandler CreateHandler() => new(
            CurrentUser.Object, Customers.Object, Notes.Object, Sales.Object,
            Drawers.Object, Sessions.Object, Stock.Object, StockMovements.Object, UnitOfWork.Object);

        public CancelCustomerCreditNoteHandler CancelHandler() => new(
            CurrentUser.Object, Customers.Object, Notes.Object, Sales.Object,
            Drawers.Object, Sessions.Object, Stock.Object, StockMovements.Object, UnitOfWork.Object);
    }

    private static CreateCustomerCreditNoteCommand CreateCommand(Fixture f, decimal amount = 50_000m) =>
        new(f.Customer.Id.Value, amount, "Bonificación acordada", new DateTime(2026, 9, 5));

    // ── EMISIÓN ──

    [Fact]
    public async Task Emision_SinVentasPendientes_TodoQuedaComoSaldoAFavor()
    {
        var f = new Fixture();

        var result = await f.CreateHandler().Handle(CreateCommand(f), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("NCC-001");
        result.Value.Imputaciones.Should().BeEmpty();
        result.Value.Sobrante.Should().Be(50_000m);
        f.Customer.CreditBalance.Should().Be(50_000m);
    }

    [Fact]
    public async Task Emision_SeImputaFifoALaVentaMasVieja()
    {
        var f = new Fixture();
        var vieja = f.CcSale(40_000m);
        var nueva = f.CcSale(80_000m);
        f.WithPendingSales(vieja, nueva);

        var result = await f.CreateHandler().Handle(CreateCommand(f), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Imputaciones.Should().HaveCount(2);
        result.Value.Imputaciones[0].Amount.Should().Be(40_000m);
        result.Value.Imputaciones[1].Amount.Should().Be(10_000m);
        result.Value.Sobrante.Should().Be(0m);
        f.Customer.CreditBalance.Should().Be(0m);
    }

    // El test que protege el diseño: si alguien vuelve a pasar null al applicator, cae.
    [Fact]
    public async Task Emision_LasImputacionesLlevanElCreditNoteId()
    {
        var f = new Fixture();
        var venta = f.CcSale(30_000m);
        f.WithPendingSales(venta);

        await f.CreateHandler().Handle(CreateCommand(f), CancellationToken.None);

        var note = f.Added.Single();
        var rows = venta.CcPayments.Where(p => p.Method == SalePaymentMethod.CustomerCredit).ToList();

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(p => p.CreditNoteId == note.Id);
        rows.Should().NotContain(p => p.CreditNoteId == Guid.Empty);
    }

    // La invariante del feature: la NC se ve en caja pero no mueve el efectivo esperado.
    [Fact]
    public async Task Emision_RegistraMovimientoNeutro_YNoAlteraElArqueo()
    {
        var f = new Fixture();
        var expectedBefore = f.Session.ExpectedClosingAmount;

        await f.CreateHandler().Handle(CreateCommand(f), CancellationToken.None);

        f.Session.ExpectedClosingAmount.Should().Be(expectedBefore);
        var movement = f.Session.Movements.Single(m => m.Type == CashMovementType.CustomerCreditNote);
        movement.Direction.Should().Be(CashMovementDirection.None);
        movement.Amount.Should().Be(50_000m);
    }

    [Fact]
    public async Task Emision_ConVentaDeOtroCliente_DevuelveSaleNotFound()
    {
        var f = new Fixture();
        var ajena = Sale.CreateCc(
            f.CompanyId, f.Branch.Id, CustomerId.New(),
            [SaleDetail.Create(ProductId.New(), 1, 10_000m)]);
        f.Sales
            .Setup(r => r.GetByIdAsync(ajena.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ajena);

        var command = CreateCommand(f) with { SaleId = ajena.Id.Value };
        var result = await f.CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Customers.CreateCreditNote.SaleNotFound");
    }

    [Fact]
    public async Task Emision_SinSesionDeCajaAbierta_DevuelveNoCashSessionOpen()
    {
        var f = new Fixture(withOpenSession: false);

        var result = await f.CreateHandler().Handle(CreateCommand(f), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Customers.CreateCreditNote.NoCashSessionOpen");
    }

    // ── ANULACIÓN ──

    [Fact]
    public async Task Anulacion_RevierteSoloSusPropiasImputaciones()
    {
        var f = new Fixture();
        var venta = f.CcSale(100_000m);

        var note = CustomerCreditNote.Create(
            f.CompanyId.Value, f.Customer.Id.Value, f.Branch.Id.Value, "NCC-001",
            20_000m, "Bonificación", new DateTime(2026, 9, 5), null, f.UserId.Value);
        var otraNota = Guid.NewGuid();
        var cobro = Guid.NewGuid();

        venta.ApplyCustomerCredit(20_000m, DateTime.UtcNow, Guid.Empty, "NC", creditNoteId: note.Id);
        venta.ApplyCustomerCredit(30_000m, DateTime.UtcNow, Guid.Empty, "Otra NC", creditNoteId: otraNota);
        venta.ApplyCustomerCredit(10_000m, DateTime.UtcNow, cobro, "Cobro");

        f.Customer.AddCredit(20_000m);
        f.Notes.Setup(r => r.GetByIdAsync(note.Id, f.CompanyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);
        f.Sales.Setup(r => r.ListByCreditNoteIdAsync(f.CompanyId, note.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Sale> { venta });

        var result = await f.CancelHandler().Handle(
            new CancelCustomerCreditNoteCommand(f.Customer.Id.Value, note.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var activas = venta.CcPayments.Where(p => p.Status == SaleCcPaymentStatus.Active).ToList();
        activas.Should().HaveCount(2);
        activas.Should().NotContain(p => p.CreditNoteId == note.Id);
        activas.Should().Contain(p => p.CreditNoteId == otraNota);
        activas.Should().Contain(p => p.CustomerPaymentId == cobro);
        note.Status.Should().Be(CreditNoteStatus.Cancelled);
    }

    [Fact]
    public async Task Anulacion_RevierteElCreditoNoConsumido()
    {
        var f = new Fixture();
        var note = CustomerCreditNote.Create(
            f.CompanyId.Value, f.Customer.Id.Value, f.Branch.Id.Value, "NCC-001",
            50_000m, "Bonificación", new DateTime(2026, 9, 5), null, f.UserId.Value);

        f.Customer.AddCredit(50_000m);
        f.Notes.Setup(r => r.GetByIdAsync(note.Id, f.CompanyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var result = await f.CancelHandler().Handle(
            new CancelCustomerCreditNoteCommand(f.Customer.Id.Value, note.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        f.Customer.CreditBalance.Should().Be(0m);
    }

    // Sin esta guarda, anular dejaría CreditBalance en negativo.
    [Fact]
    public async Task Anulacion_SinSaldoSuficiente_DevuelveCreditAlreadyConsumed()
    {
        var f = new Fixture();
        var note = CustomerCreditNote.Create(
            f.CompanyId.Value, f.Customer.Id.Value, f.Branch.Id.Value, "NCC-001",
            50_000m, "Bonificación", new DateTime(2026, 9, 5), null, f.UserId.Value);

        // El crédito se gastó por otro camino: el saldo actual no alcanza para revertirlo.
        f.Notes.Setup(r => r.GetByIdAsync(note.Id, f.CompanyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var result = await f.CancelHandler().Handle(
            new CancelCustomerCreditNoteCommand(f.Customer.Id.Value, note.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Customers.CancelCreditNote.CreditAlreadyConsumed");
        f.Customer.CreditBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Anulacion_RegistraMovimientoNeutro_YNoAlteraElArqueo()
    {
        var f = new Fixture();
        var note = CustomerCreditNote.Create(
            f.CompanyId.Value, f.Customer.Id.Value, f.Branch.Id.Value, "NCC-001",
            50_000m, "Bonificación", new DateTime(2026, 9, 5), null, f.UserId.Value);

        f.Customer.AddCredit(50_000m);
        f.Notes.Setup(r => r.GetByIdAsync(note.Id, f.CompanyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var expectedBefore = f.Session.ExpectedClosingAmount;

        await f.CancelHandler().Handle(
            new CancelCustomerCreditNoteCommand(f.Customer.Id.Value, note.Id), CancellationToken.None);

        f.Session.ExpectedClosingAmount.Should().Be(expectedBefore);
        f.Session.Movements.Should().Contain(m => m.Type == CashMovementType.CustomerCreditNoteCancellation);
    }

    [Fact]
    public async Task Anulacion_DeUnaNotaYaAnulada_DevuelveAlreadyCancelled()
    {
        var f = new Fixture();
        var note = CustomerCreditNote.Create(
            f.CompanyId.Value, f.Customer.Id.Value, f.Branch.Id.Value, "NCC-001",
            50_000m, "Bonificación", new DateTime(2026, 9, 5), null, f.UserId.Value);
        note.Cancel(f.UserId.Value);

        f.Notes.Setup(r => r.GetByIdAsync(note.Id, f.CompanyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var result = await f.CancelHandler().Handle(
            new CancelCustomerCreditNoteCommand(f.Customer.Id.Value, note.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Customers.CancelCreditNote.AlreadyCancelled");
    }
}
