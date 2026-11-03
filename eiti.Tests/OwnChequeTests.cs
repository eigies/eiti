using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Cheques.Queries.ListCheques;
using eiti.Application.Features.Cheques.Queries.ListOwnChequesDue;
using eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;
using eiti.Application.Features.Suppliers.Commands.AddSupplierPaymentOrder;
using eiti.Application.Features.Suppliers.Common;
using eiti.Domain.Banks;
using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

// Cheques propios: los que emite la empresa para pagarle a un proveedor. Nunca pasan por cartera,
// y el dia que vencen hay que avisarlo porque se debitan del banco.
public sealed class OwnChequeTests
{
    private static readonly DateTime Emision = new(2026, 10, 2);
    private static readonly DateTime Vencimiento = new(2026, 11, 2);
    private readonly CompanyId _companyId = CompanyId.New();

    private Cheque Own(Guid supplierId, int bankId = 7, string numero = "00012345", decimal monto = 300_000m) =>
        Cheque.CreateOwn(_companyId, supplierId, bankId, numero, "Baterias Soler", monto, Emision, Vencimiento, null);

    // ── Dominio ──────────────────────────────────────────────────────────────

    [Fact]
    public void CreateOwn_NaceEntregadoAlProveedor_NuncaEnCartera()
    {
        var supplierId = Guid.NewGuid();

        var cheque = Own(supplierId);

        cheque.Origen.Should().Be(ChequeOrigen.Propio);
        cheque.Estado.Should().Be(ChequeStatus.Entregado);
        cheque.SupplierId.Should().Be(supplierId);
        cheque.FechaVencimiento.Should().Be(Vencimiento);
    }

    [Fact]
    public void ReturnToCartera_NoSeAdmiteEnUnChequePropio()
    {
        var cheque = Own(Guid.NewGuid());

        cheque.Invoking(c => c.ReturnToCartera()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void VoidOwn_DejaElChequeAnulado()
    {
        var cheque = Own(Guid.NewGuid());

        cheque.VoidOwn();

        cheque.Estado.Should().Be(ChequeStatus.Anulado);
    }

    [Fact]
    public void VoidOwn_NoSeAdmiteEnUnChequeDeTercero()
    {
        var cheque = Cheque.CreateForCcPayment(_companyId, Guid.NewGuid(), 1, "1", "Cliente", "30712345678", 10m, Emision, Vencimiento, null);
        cheque.EndorseToSupplier();

        cheque.Invoking(c => c.VoidOwn()).Should().Throw<InvalidOperationException>();
    }

    // ── Validadores ──────────────────────────────────────────────────────────

    private static OwnChequeInput ValidOwn => new(7, "00012345", Emision, Vencimiento);

    private static AddSupplierPaymentOrderCommand Order(Guid? chequeId, OwnChequeInput? own) =>
        new(Guid.NewGuid(), Emision, null, [new AddSupplierPaymentOrderLine((int)PurchasePaymentMethod.Check, 100m, chequeId, null, own)]);

    [Fact]
    public void Validador_ChequeSinCarteraNiPropio_SeRechaza()
    {
        new AddSupplierPaymentOrderValidator().Validate(Order(null, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validador_ChequeConCarteraYPropioALaVez_SeRechaza()
    {
        new AddSupplierPaymentOrderValidator().Validate(Order(Guid.NewGuid(), ValidOwn)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validador_ChequePropioCompleto_Pasa()
    {
        new AddSupplierPaymentOrderValidator().Validate(Order(null, ValidOwn)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, "00012345")]
    [InlineData(7, "   ")]
    public void Validador_ChequePropioSinBancoONumero_SeRechaza(int bankId, string numero)
    {
        new AddSupplierPaymentOrderValidator()
            .Validate(Order(null, new OwnChequeInput(bankId, numero, Emision, Vencimiento)))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validador_FechaDePagoAnteriorALaEmision_SeRechaza()
    {
        new AddSupplierPaymentOrderValidator()
            .Validate(Order(null, new OwnChequeInput(7, "1", Vencimiento, Emision)))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validador_PagoSuelto_AceptaChequePropioSinChequeDeCartera()
    {
        var command = new AddSupplierPaymentCommand(
            Guid.NewGuid(), (int)PurchasePaymentMethod.Check, 100m, Emision, null, null, null, ValidOwn);

        new AddSupplierPaymentValidator().Validate(command).IsValid.Should().BeTrue();
    }

    // ── Pantalla Cheques ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListaDeCheques_FiltraPorOrigen_YMuestraElProveedorDelPropio()
    {
        var supplier = Supplier.Create(_companyId.Value, "Distribuidora Norte", null, null, null, null);
        var cheque = Own(supplier.Id);
        var cheques = new Mock<IChequeRepository>();
        cheques.Setup(r => r.ListAsync(It.IsAny<ChequeFilters>(), _companyId, It.IsAny<CancellationToken>())).ReturnsAsync([cheque]);

        var result = await ListHandler(cheques, supplier)
            .Handle(new ListChequesQuery(null, null, null, null, null, ChequeOrigen.Propio), CancellationToken.None);

        cheques.Verify(r => r.ListAsync(
            It.Is<ChequeFilters>(f => f.Origen == ChequeOrigen.Propio), _companyId, It.IsAny<CancellationToken>()), Times.Once);
        var row = result.Value.Should().ContainSingle().Subject;
        row.Origen.Should().Be((int)ChequeOrigen.Propio);
        row.SupplierName.Should().Be("Distribuidora Norte");
        row.SaleCode.Should().BeNull();
    }

    private ListChequesHandler ListHandler(Mock<IChequeRepository> cheques, Supplier supplier)
    {
        var banks = new Mock<IBankRepository>();
        banks.Setup(r => r.ListAsync(false, _companyId, It.IsAny<CancellationToken>(), It.IsAny<BankUsage>()))
            .ReturnsAsync([]);
        return new ListChequesHandler(CurrentUser().Object, cheques.Object, banks.Object, new Mock<ISaleRepository>().Object, Suppliers(supplier).Object);
    }

    // ── Aviso de vencimiento ─────────────────────────────────────────────────

    [Fact]
    public void Ventana_UnDiaDeSemana_EsSoloEseDia()
    {
        var martes = new DateTime(2026, 11, 3);

        OwnChequeDueWindow.For(martes).Should().Be((martes, martes));
    }

    [Fact]
    public void Ventana_ElLunes_SumaElFinDeSemana()
    {
        var lunes = new DateTime(2026, 11, 2);

        OwnChequeDueWindow.For(lunes).Should().Be((new DateTime(2026, 10, 31), lunes));
    }

    [Fact]
    public async Task ChequesQueVencen_SoloBuscaPropiosEntregados_ConBancoYProveedor()
    {
        var supplier = Supplier.Create(_companyId.Value, "Distribuidora Norte", null, null, null, null);
        var bank = Bank.Create(_companyId, "BANCO GALICIA");
        var cheque = Own(supplier.Id, bank.Id, "00012345", 300_000m);
        var cheques = new Mock<IChequeRepository>();
        cheques.Setup(r => r.ListAsync(It.IsAny<ChequeFilters>(), _companyId, It.IsAny<CancellationToken>())).ReturnsAsync([cheque]);
        var banks = new Mock<IBankRepository>();
        banks.Setup(r => r.ListAsync(false, _companyId, It.IsAny<CancellationToken>(), It.IsAny<BankUsage>())).ReturnsAsync([bank]);

        var result = await new ListOwnChequesDueHandler(CurrentUser().Object, cheques.Object, banks.Object, Suppliers(supplier).Object)
            .Handle(new ListOwnChequesDueQuery(), CancellationToken.None);

        cheques.Verify(r => r.ListAsync(
            It.Is<ChequeFilters>(f => f.Origen == ChequeOrigen.Propio && f.Estado == ChequeStatus.Entregado
                && f.FechaVencFrom != null && f.FechaVencTo != null),
            _companyId, It.IsAny<CancellationToken>()), Times.Once);
        var row = result.Value.Should().ContainSingle().Subject;
        row.Numero.Should().Be("00012345");
        row.BankName.Should().Be("BANCO GALICIA");
        row.SupplierName.Should().Be("Distribuidora Norte");
        row.Monto.Should().Be(300_000m);
        row.FechaVencimiento.Should().Be(Vencimiento);
    }

    private Mock<ICurrentUserService> CurrentUser()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        return currentUser;
    }

    private Mock<ISupplierRepository> Suppliers(Supplier supplier)
    {
        var suppliers = new Mock<ISupplierRepository>();
        suppliers.Setup(r => r.ListAsync(_companyId.Value, false, null, It.IsAny<CancellationToken>())).ReturnsAsync([supplier]);
        return suppliers;
    }
}
