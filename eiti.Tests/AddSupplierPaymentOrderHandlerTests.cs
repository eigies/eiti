using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;
using eiti.Application.Features.Suppliers.Commands.AddSupplierPaymentOrder;
using eiti.Application.Features.Suppliers.Common;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class AddSupplierPaymentOrderHandlerTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly BranchId _branchId = BranchId.New();
    private readonly UserId _userId = UserId.New();
    private readonly CashSession _session;
    private readonly Supplier _supplier;
    private readonly List<Purchase> _pending = [];
    private readonly Dictionary<Guid, Cheque> _cheques = [];
    private readonly List<SupplierPaymentOrder> _orders = [];
    private readonly List<SupplierPayment> _payments = [];
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private int _existingOrders;

    public AddSupplierPaymentOrderHandlerTests()
    {
        var drawer = CashDrawer.Create(_companyId, _branchId, "Caja 1");
        _session = CashSession.Open(_companyId, _branchId, drawer.Id, _userId, 200_000m, null);
        _supplier = Supplier.Create(_companyId.Value, "Distribuidora Norte", null, null, null, null);
    }

    [Fact]
    public async Task VariosMedios_UnaOrden_SoloElEfectivoMueveCaja_ChequesEntregados_ImputaFifo()
    {
        _existingOrders = 3;
        var vieja = AddPurchase("COMP-0112", 685_000m);
        var nueva = AddPurchase("COMP-0118", 640_000m);
        var chequeA = AddCheque(350_000m);
        var chequeB = AddCheque(250_000m);

        var result = await OrderHandler().Handle(
            new AddSupplierPaymentOrderCommand(
                _supplier.Id,
                new DateTime(2026, 10, 2),
                "Pago facturas septiembre",
                [
                    Line(PurchasePaymentMethod.Check, 350_000m, chequeA.Id),
                    Line(PurchasePaymentMethod.Check, 250_000m, chequeB.Id),
                    Line(PurchasePaymentMethod.BankTransfer, 150_000m, reference: "Ref. 88213"),
                    Line(PurchasePaymentMethod.Cash, 50_000m),
                ]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        response.Code.Should().Be("OP-0004");
        response.TotalAmount.Should().Be(800_000m);
        response.PaymentIds.Should().HaveCount(4);

        var order = _orders.Single();
        _payments.Should().HaveCount(4).And.OnlyContain(p => p.PaymentOrderId == order.Id);
        _payments.Single(p => p.Method == PurchasePaymentMethod.BankTransfer).Reference.Should().Be("Ref. 88213");

        chequeA.Estado.Should().Be(ChequeStatus.Entregado);
        chequeB.Estado.Should().Be(ChequeStatus.Entregado);

        _session.Movements.Where(m => m.Type == CashMovementType.PurchaseExpense).Should().HaveCount(4);
        _session.ExpectedClosingAmount.Should().Be(150_000m, "solo los 50.000 en efectivo salen del cajón");

        vieja.PendingAmount.Should().Be(0m);
        nueva.PendingAmount.Should().Be(525_000m);
        response.Imputaciones.Select(i => (i.Code, i.Amount)).Should().Equal(
            ("COMP-0112", 685_000m),
            ("COMP-0118", 115_000m));
        response.AppliedToPurchases.Should().Be(800_000m);
        response.CreditAdded.Should().Be(0m);
        _supplier.CreditBalance.Should().Be(0m);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PagoMayorAlPendiente_ElExcedenteQuedaComoSaldoAFavor()
    {
        AddPurchase("COMP-0125", 640_000m);

        var result = await OrderHandler().Handle(
            new AddSupplierPaymentOrderCommand(
                _supplier.Id, new DateTime(2026, 10, 14), null,
                [Line(PurchasePaymentMethod.Cash, 600_000m), Line(PurchasePaymentMethod.BankTransfer, 400_000m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AppliedToPurchases.Should().Be(640_000m);
        result.Value.CreditAdded.Should().Be(360_000m);
        _supplier.CreditBalance.Should().Be(360_000m);
    }

    [Fact]
    public async Task UnChequeQueNoEstaEnCartera_NoRegistraNada()
    {
        AddPurchase("COMP-0112", 685_000m);
        var bueno = AddCheque(350_000m);
        var entregado = AddCheque(250_000m);
        entregado.EndorseToSupplier();

        var result = await OrderHandler().Handle(
            new AddSupplierPaymentOrderCommand(
                _supplier.Id, new DateTime(2026, 10, 2), null,
                [
                    Line(PurchasePaymentMethod.Cash, 50_000m),
                    Line(PurchasePaymentMethod.Check, 350_000m, bueno.Id),
                    Line(PurchasePaymentMethod.Check, 250_000m, entregado.Id),
                ]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SupplierPaymentLineErrors.ChequeNotEnCartera);
        _orders.Should().BeEmpty();
        _payments.Should().BeEmpty();
        _session.Movements.Should().OnlyContain(m => m.Type == CashMovementType.OpeningFloat);
        bueno.Estado.Should().Be(ChequeStatus.EnCartera);
        _supplier.CreditBalance.Should().Be(0m);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ElMismoChequeDosVeces_SeRechaza()
    {
        var cheque = AddCheque(100_000m);

        var result = await OrderHandler().Handle(
            new AddSupplierPaymentOrderCommand(
                _supplier.Id, new DateTime(2026, 10, 2), null,
                [Line(PurchasePaymentMethod.Check, 100_000m, cheque.Id), Line(PurchasePaymentMethod.Check, 100_000m, cheque.Id)]),
            CancellationToken.None);

        result.Error.Should().Be(SupplierPaymentLineErrors.DuplicateCheque);
        _payments.Should().BeEmpty();
    }

    [Fact]
    public void Validador_RechazaSinMedios_ChequeRepetidoYSaldoAFavorComoMedio()
    {
        var validator = new AddSupplierPaymentOrderValidator();
        var chequeId = Guid.NewGuid();

        validator.Validate(new AddSupplierPaymentOrderCommand(Guid.NewGuid(), DateTime.Today, null, []))
            .IsValid.Should().BeFalse();
        validator.Validate(new AddSupplierPaymentOrderCommand(Guid.NewGuid(), DateTime.Today, null,
                [Line(PurchasePaymentMethod.Check, 1m, chequeId), Line(PurchasePaymentMethod.Check, 1m, chequeId)]))
            .IsValid.Should().BeFalse();
        validator.Validate(new AddSupplierPaymentOrderCommand(Guid.NewGuid(), DateTime.Today, null,
                [Line(PurchasePaymentMethod.SupplierCredit, 1m)]))
            .IsValid.Should().BeFalse();
        validator.Validate(new AddSupplierPaymentOrderCommand(Guid.NewGuid(), DateTime.Today, null,
                [Line(PurchasePaymentMethod.Check, 1m)]))
            .IsValid.Should().BeFalse("con cheque hay que elegir el cheque de cartera");
        validator.Validate(new AddSupplierPaymentOrderCommand(Guid.NewGuid(), DateTime.Today, null,
                [Line(PurchasePaymentMethod.Cash, 1m), Line(PurchasePaymentMethod.Check, 1m, chequeId)]))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PagoSuelto_QuedaComoOrdenDeUnMedio_YRespondeIgualQueAntes()
    {
        AddPurchase("COMP-0112", 100_000m);

        var result = await SingleHandler().Handle(
            new AddSupplierPaymentCommand(
                _supplier.Id, (int)PurchasePaymentMethod.Cash, 150_000m, new DateTime(2026, 10, 2), "Recibo 12", "nota", null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var payment = _payments.Single();
        var order = _orders.Single();
        order.Code.Should().Be("OP-0001");
        order.TotalAmount.Should().Be(150_000m);
        payment.PaymentOrderId.Should().Be(order.Id);
        result.Value.PaymentId.Should().Be(payment.Id);
        result.Value.Amount.Should().Be(150_000m);
        result.Value.AppliedToPurchases.Should().Be(100_000m);
        result.Value.CreditAdded.Should().Be(50_000m);
        result.Value.SupplierCreditBalance.Should().Be(50_000m);
    }

    private static AddSupplierPaymentOrderLine Line(
        PurchasePaymentMethod method, decimal amount, Guid? chequeId = null, string? reference = null) =>
        new((int)method, amount, chequeId, reference);

    private Purchase AddPurchase(string code, decimal total)
    {
        var purchase = Purchase.Create(
            _companyId.Value,
            _branchId.Value,
            _supplier.Id,
            [PurchaseDetail.Create(Guid.NewGuid(), "Producto", 1, total)],
            null,
            null,
            _userId.Value,
            code);
        _pending.Add(purchase);
        return purchase;
    }

    private Cheque AddCheque(decimal monto)
    {
        var cheque = Cheque.CreateForCcPayment(
            _companyId, Guid.NewGuid(), 1, $"{_cheques.Count + 1:D8}", "Ferretería Sur", "30712345678",
            monto, new DateTime(2026, 9, 1), new DateTime(2026, 11, 10), null);
        _cheques[cheque.Id] = cheque;
        return cheque;
    }

    private SupplierPaymentOrderService Service()
    {
        var supplierRepository = new Mock<ISupplierRepository>();
        var paymentRepository = new Mock<ISupplierPaymentRepository>();
        paymentRepository
            .Setup(r => r.AddAsync(It.IsAny<SupplierPayment>(), It.IsAny<CancellationToken>()))
            .Callback<SupplierPayment, CancellationToken>((p, _) => _payments.Add(p))
            .Returns(Task.CompletedTask);

        var orderRepository = new Mock<ISupplierPaymentOrderRepository>();
        orderRepository
            .Setup(r => r.CountByCompanyAsync(_companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _existingOrders);
        orderRepository
            .Setup(r => r.AddAsync(It.IsAny<SupplierPaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback<SupplierPaymentOrder, CancellationToken>((o, _) => _orders.Add(o))
            .Returns(Task.CompletedTask);

        var purchaseRepository = new Mock<IPurchaseRepository>();
        purchaseRepository
            .Setup(r => r.ListPendingBySupplierAsync(_companyId.Value, _supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _pending.ToList());

        var chequeRepository = new Mock<IChequeRepository>();
        chequeRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CompanyId _, CancellationToken _) => _cheques.GetValueOrDefault(id));

        return new SupplierPaymentOrderService(
            supplierRepository.Object,
            paymentRepository.Object,
            orderRepository.Object,
            purchaseRepository.Object,
            chequeRepository.Object);
    }

    private (Mock<ICurrentUserService>, Mock<ISupplierRepository>, Mock<ICashDrawerRepository>, Mock<ICashSessionRepository>) Context()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        currentUser.SetupGet(x => x.UserId).Returns(_userId);

        var suppliers = new Mock<ISupplierRepository>();
        suppliers
            .Setup(r => r.GetByIdAsync(_supplier.Id, _companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_supplier);

        var drawers = new Mock<ICashDrawerRepository>();
        drawers
            .Setup(r => r.GetByAssignedUserAsync(_userId, _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashDrawer.Create(_companyId, _branchId, "Caja 1"));

        var sessions = new Mock<ICashSessionRepository>();
        sessions
            .Setup(r => r.GetOpenByDrawerAsync(It.IsAny<CashDrawerId>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_session);

        return (currentUser, suppliers, drawers, sessions);
    }

    private AddSupplierPaymentOrderHandler OrderHandler()
    {
        var (currentUser, suppliers, drawers, sessions) = Context();
        return new AddSupplierPaymentOrderHandler(
            currentUser.Object, suppliers.Object, drawers.Object, sessions.Object, Service(), _unitOfWork.Object);
    }

    private AddSupplierPaymentHandler SingleHandler()
    {
        var (currentUser, suppliers, drawers, sessions) = Context();
        return new AddSupplierPaymentHandler(
            currentUser.Object, suppliers.Object, drawers.Object, sessions.Object, Service(), _unitOfWork.Object);
    }
}
