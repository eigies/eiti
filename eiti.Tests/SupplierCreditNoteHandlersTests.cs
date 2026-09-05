using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Suppliers.Commands.CancelSupplierCreditNote;
using eiti.Application.Features.Suppliers.Commands.CreateSupplierCreditNote;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Common;
using eiti.Domain.Companies;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class SupplierCreditNoteHandlersTests
{
    private sealed class Fixture
    {
        public CompanyId CompanyId { get; } = CompanyId.New();
        public UserId UserId { get; } = UserId.New();
        public Branch Branch { get; }
        public CashSession Session { get; }
        public Supplier Supplier { get; }

        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<ISupplierRepository> Suppliers { get; } = new();
        public Mock<ISupplierCreditNoteRepository> Notes { get; } = new();
        public Mock<IPurchaseRepository> Purchases { get; } = new();
        public Mock<ICashDrawerRepository> Drawers { get; } = new();
        public Mock<ICashSessionRepository> Sessions { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public List<SupplierCreditNote> Added { get; } = [];

        public Fixture(bool withOpenSession = true)
        {
            Branch = Branch.Create(CompanyId, "Sucursal Centro", "SC", "San Martin 123");
            var drawer = CashDrawer.Create(CompanyId, Branch.Id, "Caja 1");
            Session = CashSession.Open(CompanyId, Branch.Id, drawer.Id, UserId, 0m, null);
            Supplier = Supplier.Create(CompanyId.Value, "Proveedor SA", null, null, null, null);

            CurrentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
            CurrentUser.SetupGet(s => s.CompanyId).Returns(CompanyId);
            CurrentUser.SetupGet(s => s.UserId).Returns(UserId);

            Suppliers
                .Setup(r => r.GetByIdAsync(Supplier.Id, CompanyId.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Supplier);

            Drawers
                .Setup(r => r.GetByAssignedUserAsync(UserId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(drawer);

            Sessions
                .Setup(r => r.GetOpenByDrawerAsync(drawer.Id, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(withOpenSession ? Session : null);

            Purchases
                .Setup(r => r.ListPendingBySupplierAsync(CompanyId.Value, Supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Purchase>());

            Purchases
                .Setup(r => r.ListByCreditNoteIdAsync(CompanyId.Value, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Purchase>());

            Notes
                .Setup(r => r.CountByBranchAsync(CompanyId.Value, Branch.Id.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            Notes
                .Setup(r => r.AddAsync(It.IsAny<SupplierCreditNote>(), It.IsAny<CancellationToken>()))
                .Callback<SupplierCreditNote, CancellationToken>((n, _) => Added.Add(n))
                .Returns(Task.CompletedTask);
        }

        public Purchase PendingPurchase(decimal total, string code)
        {
            return Purchase.Create(
                CompanyId.Value, Branch.Id.Value, Supplier.Id,
                [PurchaseDetail.Create(Guid.NewGuid(), "Producto", 1m, total)],
                null, null, UserId.Value, code);
        }

        public void WithPendingPurchases(params Purchase[] purchases)
        {
            Purchases
                .Setup(r => r.ListPendingBySupplierAsync(CompanyId.Value, Supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(purchases.ToList());
        }

        public SupplierCreditNote Note(decimal amount = 50_000m)
        {
            return SupplierCreditNote.Create(
                CompanyId.Value, Supplier.Id, Branch.Id.Value, "NCP-001",
                amount, "Bonificación del proveedor", new DateTime(2026, 9, 5), null, UserId.Value);
        }

        public CreateSupplierCreditNoteHandler CreateHandler() => new(
            CurrentUser.Object, Suppliers.Object, Notes.Object, Purchases.Object,
            Drawers.Object, Sessions.Object, UnitOfWork.Object);

        public CancelSupplierCreditNoteHandler CancelHandler() => new(
            CurrentUser.Object, Suppliers.Object, Notes.Object, Purchases.Object,
            Drawers.Object, Sessions.Object, UnitOfWork.Object);
    }

    private static CreateSupplierCreditNoteCommand CreateCommand(Fixture f, decimal amount = 50_000m) =>
        new(f.Supplier.Id, amount, "Bonificación del proveedor", new DateTime(2026, 9, 5));

    // ── EMISIÓN ──

    [Fact]
    public async Task Emision_SinComprasPendientes_TodoQuedaComoSaldoAFavor()
    {
        var f = new Fixture();

        var result = await f.CreateHandler().Handle(CreateCommand(f), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("NCP-001");
        result.Value.Imputaciones.Should().BeEmpty();
        result.Value.Sobrante.Should().Be(50_000m);
        f.Supplier.CreditBalance.Should().Be(50_000m);
    }

    [Fact]
    public async Task Emision_SeImputaFifoALaCompraMasVieja()
    {
        var f = new Fixture();
        var vieja = f.PendingPurchase(40_000m, "COMP-001");
        var nueva = f.PendingPurchase(80_000m, "COMP-002");
        f.WithPendingPurchases(vieja, nueva);

        var result = await f.CreateHandler().Handle(CreateCommand(f), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Imputaciones.Should().HaveCount(2);
        result.Value.Imputaciones[0].Amount.Should().Be(40_000m);
        result.Value.Imputaciones[1].Amount.Should().Be(10_000m);
        f.Supplier.CreditBalance.Should().Be(0m);
    }

    // El test que protege el diseño: si alguien vuelve a pasar null al applicator, cae.
    [Fact]
    public async Task Emision_LasImputacionesLlevanElCreditNoteId()
    {
        var f = new Fixture();
        var compra = f.PendingPurchase(30_000m, "COMP-001");
        f.WithPendingPurchases(compra);

        await f.CreateHandler().Handle(CreateCommand(f), CancellationToken.None);

        var note = f.Added.Single();
        var rows = compra.Payments
            .Where(p => p.Method == PurchasePaymentMethod.SupplierCredit)
            .ToList();

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(p => p.CreditNoteId == note.Id);
    }

    [Fact]
    public async Task Emision_RegistraMovimientoNeutro_YNoAlteraElArqueo()
    {
        var f = new Fixture();
        var expectedBefore = f.Session.ExpectedClosingAmount;

        await f.CreateHandler().Handle(CreateCommand(f), CancellationToken.None);

        f.Session.ExpectedClosingAmount.Should().Be(expectedBefore);
        var movement = f.Session.Movements.Single(m => m.Type == CashMovementType.SupplierCreditNote);
        movement.Direction.Should().Be(CashMovementDirection.None);
    }

    [Fact]
    public async Task Emision_SinSesionDeCajaAbierta_DevuelveNoCashSessionOpen()
    {
        var f = new Fixture(withOpenSession: false);

        var result = await f.CreateHandler().Handle(CreateCommand(f), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Suppliers.CreateCreditNote.NoCashSessionOpen");
    }

    // ── ANULACIÓN ──

    [Fact]
    public async Task Anulacion_RevierteSoloSusPropiasImputaciones()
    {
        var f = new Fixture();
        var compra = f.PendingPurchase(100_000m, "COMP-001");
        var note = f.Note(20_000m);
        var otraNota = Guid.NewGuid();
        var pago = Guid.NewGuid();

        compra.AddPayment(PurchasePayment.Create(
            PurchasePaymentMethod.SupplierCredit, 20_000m, DateTime.UtcNow, null, null,
            creditNoteId: note.Id));
        compra.AddPayment(PurchasePayment.Create(
            PurchasePaymentMethod.SupplierCredit, 30_000m, DateTime.UtcNow, null, null,
            creditNoteId: otraNota));
        compra.AddPayment(PurchasePayment.Create(
            PurchasePaymentMethod.SupplierCredit, 10_000m, DateTime.UtcNow, null, null,
            supplierPaymentId: pago));

        f.Supplier.AddCredit(20_000m);
        f.Notes.Setup(r => r.GetByIdAsync(note.Id, f.CompanyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);
        f.Purchases.Setup(r => r.ListByCreditNoteIdAsync(f.CompanyId.Value, note.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Purchase> { compra });

        var result = await f.CancelHandler().Handle(
            new CancelSupplierCreditNoteCommand(f.Supplier.Id, note.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var activas = compra.Payments.Where(p => p.Status == PurchasePaymentStatus.Active).ToList();
        activas.Should().HaveCount(2);
        activas.Should().NotContain(p => p.CreditNoteId == note.Id);
        activas.Should().Contain(p => p.CreditNoteId == otraNota);
        activas.Should().Contain(p => p.SupplierPaymentId == pago);
        note.Status.Should().Be(CreditNoteStatus.Cancelled);
    }

    [Fact]
    public async Task Anulacion_RevierteElCreditoNoConsumido()
    {
        var f = new Fixture();
        var note = f.Note();
        f.Supplier.AddCredit(50_000m);
        f.Notes.Setup(r => r.GetByIdAsync(note.Id, f.CompanyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var result = await f.CancelHandler().Handle(
            new CancelSupplierCreditNoteCommand(f.Supplier.Id, note.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        f.Supplier.CreditBalance.Should().Be(0m);
    }

    // Sin esta guarda, anular dejaría CreditBalance en negativo.
    [Fact]
    public async Task Anulacion_SinSaldoSuficiente_DevuelveCreditAlreadyConsumed()
    {
        var f = new Fixture();
        var note = f.Note();
        f.Notes.Setup(r => r.GetByIdAsync(note.Id, f.CompanyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var result = await f.CancelHandler().Handle(
            new CancelSupplierCreditNoteCommand(f.Supplier.Id, note.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Suppliers.CancelCreditNote.CreditAlreadyConsumed");
        f.Supplier.CreditBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Anulacion_DeUnaNotaYaAnulada_DevuelveAlreadyCancelled()
    {
        var f = new Fixture();
        var note = f.Note();
        note.Cancel(f.UserId.Value);
        f.Notes.Setup(r => r.GetByIdAsync(note.Id, f.CompanyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var result = await f.CancelHandler().Handle(
            new CancelSupplierCreditNoteCommand(f.Supplier.Id, note.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Suppliers.CancelCreditNote.AlreadyCancelled");
    }
}
