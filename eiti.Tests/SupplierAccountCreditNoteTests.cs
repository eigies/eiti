using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Suppliers.Queries.GetSupplierAccount;
using eiti.Domain.Companies;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

// El estado de cuenta del proveedor deriva `pagadoTotal` por resta
// (deudaTotal - saldoPendiente), asi que una nota de credito lo inflaba: bajaba el pendiente
// y por lo tanto subia lo "pagado", diciendo que se le pago al proveedor plata que nadie pago.
public sealed class SupplierAccountCreditNoteTests
{
    private sealed class Fixture
    {
        public CompanyId CompanyId { get; } = CompanyId.New();
        public UserId UserId { get; } = UserId.New();
        public Guid BranchId { get; } = Guid.NewGuid();
        public Supplier Supplier { get; }

        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<ISupplierRepository> Suppliers { get; } = new();
        public Mock<ISupplierPaymentRepository> Payments { get; } = new();
        public Mock<ISupplierCreditNoteRepository> Notes { get; } = new();
        public Mock<IPurchaseRepository> Purchases { get; } = new();
        public Mock<IChequeRepository> Cheques { get; } = new();

        public Fixture()
        {
            Supplier = Supplier.Create(CompanyId.Value, "Proveedor SA", null, null, null, null);

            CurrentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
            CurrentUser.SetupGet(s => s.CompanyId).Returns(CompanyId);

            Suppliers
                .Setup(r => r.GetByIdAsync(Supplier.Id, CompanyId.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Supplier);

            Payments
                .Setup(r => r.ListBySupplierAsync(CompanyId.Value, Supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SupplierPayment>());

            Notes
                .Setup(r => r.ListBySupplierAsync(CompanyId.Value, Supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SupplierCreditNote>());

            Purchases
                .Setup(r => r.ListAllBySupplierAsync(CompanyId.Value, Supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Purchase>());
        }

        public Purchase Purchase(decimal total, string code) =>
            Domain.Purchases.Purchase.Create(
                CompanyId.Value, BranchId, Supplier.Id,
                [PurchaseDetail.Create(Guid.NewGuid(), "Producto", 1m, total)],
                null, null, UserId.Value, code);

        public SupplierCreditNote Note(decimal amount) =>
            SupplierCreditNote.Create(
                CompanyId.Value, Supplier.Id, BranchId, "NCP-001",
                amount, "Bonificación del proveedor", new DateTime(2026, 9, 5), null, UserId.Value);

        public void With(IEnumerable<Purchase> purchases, IEnumerable<SupplierCreditNote> notes)
        {
            Purchases
                .Setup(r => r.ListAllBySupplierAsync(CompanyId.Value, Supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(purchases.ToList());
            Notes
                .Setup(r => r.ListBySupplierAsync(CompanyId.Value, Supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(notes.ToList());
        }

        public GetSupplierAccountHandler Handler() => new(
            CurrentUser.Object, Suppliers.Object, Payments.Object, Notes.Object,
            Purchases.Object, Cheques.Object);

        public Task<Application.Common.Result<GetSupplierAccountResponse>> Run() =>
            Handler().Handle(new GetSupplierAccountQuery(Supplier.Id), CancellationToken.None);
    }

    // LA invariante: la NC baja lo que se debe, pero no es plata pagada.
    [Fact]
    public async Task NotaDeCredito_BajaElPendiente_YNoInflaLoPagado()
    {
        var f = new Fixture();
        var compra = f.Purchase(100_000m, "COMP-001");
        var note = f.Note(30_000m);
        compra.AddPayment(PurchasePayment.Create(
            PurchasePaymentMethod.SupplierCredit, 30_000m, DateTime.UtcNow, null, null,
            creditNoteId: note.Id));
        f.With([compra], [note]);

        var result = await f.Run();

        result.IsSuccess.Should().BeTrue();
        result.Value!.DeudaTotal.Should().Be(100_000m);
        result.Value.SaldoPendiente.Should().Be(70_000m);
        // Sin descontar la NC daría 30.000: diría que se le pagó al proveedor plata
        // que nadie pagó.
        result.Value.PagadoTotal.Should().Be(0m);
    }

    [Fact]
    public async Task NotaDeCredito_ApareceComoMovimientoPropio()
    {
        var f = new Fixture();
        var compra = f.Purchase(100_000m, "COMP-001");
        var note = f.Note(30_000m);
        compra.AddPayment(PurchasePayment.Create(
            PurchasePaymentMethod.SupplierCredit, 30_000m, DateTime.UtcNow, null, null,
            creditNoteId: note.Id));
        f.With([compra], [note]);

        var result = await f.Run();

        var movimiento = result.Value!.Movements.Single(m => m.Type == "nota_credito");
        movimiento.Code.Should().Be("NCP-001");
        movimiento.Amount.Should().Be(30_000m);
        movimiento.IsDebit.Should().BeFalse();
        movimiento.Description.Should().Be("Bonificación del proveedor");
        movimiento.Imputaciones.Should().ContainSingle()
            .Which.Code.Should().Be("COMP-001");
    }

    [Fact]
    public async Task NotaDeCredito_SinImputar_QuedaEnteraComoSobrante()
    {
        var f = new Fixture();
        var note = f.Note(30_000m);
        f.With([], [note]);

        var result = await f.Run();

        var movimiento = result.Value!.Movements.Single(m => m.Type == "nota_credito");
        movimiento.Sobrante.Should().Be(30_000m);
        movimiento.Imputaciones.Should().BeEmpty();
    }

    [Fact]
    public async Task NotaDeCreditoAnulada_NoAparece_NiAfectaLosTotales()
    {
        var f = new Fixture();
        var compra = f.Purchase(100_000m, "COMP-001");
        var note = f.Note(30_000m);
        note.Cancel(f.UserId.Value);
        f.With([compra], [note]);

        var result = await f.Run();

        result.Value!.Movements.Should().NotContain(m => m.Type == "nota_credito");
        result.Value.SaldoPendiente.Should().Be(100_000m);
        result.Value.PagadoTotal.Should().Be(0m);
    }
}
