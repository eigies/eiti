using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Domain.Banks;
using eiti.Application.Features.Suppliers.Queries.GetSupplierLedger;
using eiti.Application.Common;
using eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;
using eiti.Application.Features.Suppliers.Commands.AddSupplierPaymentOrder;
using eiti.Application.Features.Suppliers.Commands.CancelSupplierPayment;
using eiti.Application.Features.Suppliers.Commands.CancelSupplierPaymentOrder;
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

public sealed class SupplierPaymentOrderHandlersTests
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
    private readonly Mock<ISupplierPaymentRepository> _paymentRepository = new();
    private readonly Mock<ISupplierPaymentOrderRepository> _orderRepository = new();
    private int _existingOrders;

    public SupplierPaymentOrderHandlersTests()
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
        _paymentRepository
            .Setup(r => r.AddAsync(It.IsAny<SupplierPayment>(), It.IsAny<CancellationToken>()))
            .Callback<SupplierPayment, CancellationToken>((p, _) => _payments.Add(p))
            .Returns(Task.CompletedTask);
        _paymentRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), _companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid _, CancellationToken _) => _payments.FirstOrDefault(p => p.Id == id));
        _paymentRepository
            .Setup(r => r.ListByOrderAsync(It.IsAny<Guid>(), _companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid orderId, Guid _, CancellationToken _) => _payments.Where(p => p.PaymentOrderId == orderId).ToList());

        _orderRepository
            .Setup(r => r.CountByCompanyAsync(_companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _existingOrders + _orders.Count);
        _orderRepository
            .Setup(r => r.AddAsync(It.IsAny<SupplierPaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback<SupplierPaymentOrder, CancellationToken>((o, _) => _orders.Add(o))
            .Returns(Task.CompletedTask);
        _orderRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), _companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid _, CancellationToken _) => _orders.FirstOrDefault(o => o.Id == id));

        var purchaseRepository = new Mock<IPurchaseRepository>();
        purchaseRepository
            .Setup(r => r.ListPendingBySupplierAsync(_companyId.Value, _supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _pending.ToList());
        purchaseRepository
            .Setup(r => r.ListBySupplierPaymentIdAsync(_companyId.Value, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid paymentId, CancellationToken _) =>
                _pending.Where(p => p.Payments.Any(x => x.SupplierPaymentId == paymentId)).ToList());

        var chequeRepository = new Mock<IChequeRepository>();
        chequeRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CompanyId _, CancellationToken _) => _cheques.GetValueOrDefault(id));

        return new SupplierPaymentOrderService(
            new Mock<ISupplierRepository>().Object,
            _paymentRepository.Object,
            _orderRepository.Object,
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

    private CancelSupplierPaymentOrderHandler CancelOrderHandler()
    {
        var (currentUser, suppliers, drawers, sessions) = Context();
        var service = Service();
        return new CancelSupplierPaymentOrderHandler(
            currentUser.Object, suppliers.Object, _orderRepository.Object, _paymentRepository.Object,
            drawers.Object, sessions.Object, service, _unitOfWork.Object);
    }

    private CancelSupplierPaymentHandler CancelPaymentHandler()
    {
        var (currentUser, suppliers, drawers, sessions) = Context();
        var service = Service();
        return new CancelSupplierPaymentHandler(
            currentUser.Object, suppliers.Object, _paymentRepository.Object,
            drawers.Object, sessions.Object, service, _unitOfWork.Object);
    }

    // ── Anulación ────────────────────────────────────────────────────────────

    private async Task<SupplierPaymentOrder> PayWithFourMeansAsync()
    {
        _existingOrders = 0;
        var result = await OrderHandler().Handle(
            new AddSupplierPaymentOrderCommand(
                _supplier.Id, new DateTime(2026, 10, 2), null,
                [
                    Line(PurchasePaymentMethod.Check, 350_000m, AddCheque(350_000m).Id),
                    Line(PurchasePaymentMethod.Check, 250_000m, AddCheque(250_000m).Id),
                    Line(PurchasePaymentMethod.BankTransfer, 150_000m),
                    Line(PurchasePaymentMethod.Cash, 300_000m),
                ]),
            CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return _orders.Single();
    }

    [Fact]
    public async Task AnularOrden_RevierteTodosLosMedios_CajaChequesFacturasYSaldoAFavor()
    {
        var vieja = AddPurchase("COMP-0112", 685_000m);
        var nueva = AddPurchase("COMP-0118", 200_000m);
        var order = await PayWithFourMeansAsync();
        _supplier.CreditBalance.Should().Be(165_000m, "1.050.000 pagados contra 885.000 pendientes");
        _session.ExpectedClosingAmount.Should().Be(-100_000m);

        var result = await CancelOrderHandler().Handle(
            new CancelSupplierPaymentOrderCommand(_supplier.Id, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(PurchasePaymentStatus.Cancelled);
        order.CancelledByUserId.Should().Be(_userId.Value);
        _payments.Should().OnlyContain(p => p.Status == PurchasePaymentStatus.Cancelled);
        _cheques.Values.Should().OnlyContain(c => c.Estado == ChequeStatus.EnCartera);
        vieja.PendingAmount.Should().Be(685_000m);
        nueva.PendingAmount.Should().Be(200_000m);
        _supplier.CreditBalance.Should().Be(0m);
        _session.ExpectedClosingAmount.Should().Be(200_000m, "vuelve el efectivo y queda el fondo inicial");
    }

    [Fact]
    public async Task AnularOrden_YaAnulada_OdeOtroProveedor_SeRechaza()
    {
        AddPurchase("COMP-0112", 685_000m);
        var order = await PayWithFourMeansAsync();

        (await CancelOrderHandler().Handle(new CancelSupplierPaymentOrderCommand(Guid.NewGuid(), order.Id), CancellationToken.None))
            .Error.Should().Be(CancelSupplierPaymentOrderErrors.NotFound);

        (await CancelOrderHandler().Handle(new CancelSupplierPaymentOrderCommand(_supplier.Id, order.Id), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        (await CancelOrderHandler().Handle(new CancelSupplierPaymentOrderCommand(_supplier.Id, order.Id), CancellationToken.None))
            .Error.Should().Be(CancelSupplierPaymentOrderErrors.AlreadyCancelled);
    }

    [Fact]
    public async Task AnularUnMedioSuelto_DeUnaOrdenConVariosMedios_SeRechazaYNoTocaNada()
    {
        var vieja = AddPurchase("COMP-0112", 685_000m);
        var order = await PayWithFourMeansAsync();
        var cash = _payments.Single(p => p.Method == PurchasePaymentMethod.Cash);

        var result = await CancelPaymentHandler().Handle(
            new CancelSupplierPaymentCommand(_supplier.Id, cash.Id), CancellationToken.None);

        result.Error.Should().Be(CancelSupplierPaymentErrors.PartOfOrder);
        cash.Status.Should().Be(PurchasePaymentStatus.Active);
        order.Status.Should().Be(PurchasePaymentStatus.Active);
        vieja.PendingAmount.Should().Be(0m);
    }

    [Fact]
    public async Task AnularPagoDeUnSoloMedio_AnulaTambienSuOrden()
    {
        var compra = AddPurchase("COMP-0112", 100_000m);
        (await SingleHandler().Handle(
            new AddSupplierPaymentCommand(_supplier.Id, (int)PurchasePaymentMethod.BankTransfer, 100_000m, new DateTime(2026, 10, 2), null, null, null),
            CancellationToken.None)).IsSuccess.Should().BeTrue();
        var payment = _payments.Single();

        var result = await CancelPaymentHandler().Handle(
            new CancelSupplierPaymentCommand(_supplier.Id, payment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PurchasePaymentStatus.Cancelled);
        _orders.Single().Status.Should().Be(PurchasePaymentStatus.Cancelled);
        compra.PendingAmount.Should().Be(100_000m);
    }

    // ── Extracto ─────────────────────────────────────────────────────────────

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private GetSupplierLedgerHandler LedgerHandler(IReadOnlyList<SupplierCreditNote> notes)
    {
        var (currentUser, suppliers, _, _) = Context();

        var purchases = new Mock<IPurchaseRepository>();
        purchases
            .Setup(r => r.ListAllBySupplierAsync(_companyId.Value, _supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _pending.ToList());
        _paymentRepository
            .Setup(r => r.ListBySupplierAsync(_companyId.Value, _supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _payments.ToList());
        _orderRepository
            .Setup(r => r.ListBySupplierAsync(_companyId.Value, _supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _orders.ToList());
        var creditNotes = new Mock<ISupplierCreditNoteRepository>();
        creditNotes
            .Setup(r => r.ListBySupplierAsync(_companyId.Value, _supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => notes.ToList());
        var cheques = new Mock<IChequeRepository>();
        cheques
            .Setup(r => r.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CompanyId _, CancellationToken _) =>
                _cheques.Values.Where(c => ids.Contains(c.Id)).ToList());
        var nacion = Bank.Create(_companyId, "Banco Nación", useForCard: true, useForTransfer: true, useForCheque: true);
        typeof(Bank).GetProperty(nameof(Bank.Id))!.SetValue(nacion, 1); // el Id lo pone la base
        var banks = new Mock<IBankRepository>();
        banks
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([nacion]);

        return new GetSupplierLedgerHandler(
            currentUser.Object, suppliers.Object, purchases.Object, _paymentRepository.Object, _orderRepository.Object,
            creditNotes.Object, cheques.Object, banks.Object, new FixedTime(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Extracto_UnRenglonPorOrden_ConMediosEImputaciones_YCuadraConLasTarjetas()
    {
        var today = BusinessCalendar.ToBusinessDate(DateTime.UtcNow);

        // Compra pagada en la compra misma, antes de la cuenta corriente.
        var legacyPurchase = AddPurchase("COMP-0099", 50_000m);
        legacyPurchase.AddPayment(PurchasePayment.Create(PurchasePaymentMethod.Cash, 50_000m, today.AddDays(-30), "Recibo 7", null));
        AddPurchase("COMP-0112", 685_000m);
        AddPurchase("COMP-0118", 200_000m);

        var chequeA = AddCheque(350_000m);
        (await OrderHandler().Handle(
            new AddSupplierPaymentOrderCommand(
                _supplier.Id, today, null,
                [
                    Line(PurchasePaymentMethod.Check, 350_000m, chequeA.Id),
                    Line(PurchasePaymentMethod.Check, 250_000m, AddCheque(250_000m).Id),
                    Line(PurchasePaymentMethod.BankTransfer, 150_000m, reference: "Ref. 88213"),
                    Line(PurchasePaymentMethod.Cash, 300_000m),
                ]),
            CancellationToken.None)).IsSuccess.Should().BeTrue();

        // Pago anterior a las órdenes (sin grupo): quedó todo como saldo a favor.
        var oldPayment = SupplierPayment.Create(
            _companyId.Value, _supplier.Id, _branchId.Value, PurchasePaymentMethod.BankTransfer, 10_000m,
            today.AddDays(-20), "Transf 1", null, _userId.Value);
        _payments.Add(oldPayment);
        _supplier.AddCredit(10_000m);

        var note = SupplierCreditNote.Create(
            _companyId.Value, _supplier.Id, _branchId.Value, "NCP-001", 20_000m, "Bonificación", today.AddDays(-5), null, _userId.Value);
        _supplier.AddCredit(20_000m);

        // Una orden anulada no mueve el saldo.
        (await OrderHandler().Handle(
            new AddSupplierPaymentOrderCommand(_supplier.Id, today, null, [Line(PurchasePaymentMethod.BankTransfer, 5_000m)]),
            CancellationToken.None)).IsSuccess.Should().BeTrue();
        var anulada = _orders.Last();
        (await CancelOrderHandler().Handle(new CancelSupplierPaymentOrderCommand(_supplier.Id, anulada.Id), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var pendiente = AddPurchase("COMP-0130", 400_000m);

        var result = await LedgerHandler([note]).Handle(new GetSupplierLedgerQuery(_supplier.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var ledger = result.Value;

        // Control de cuadre: el saldo final del extracto es lo que dicen las tarjetas.
        ledger.ClosingBalance.Should().Be(_pending.Sum(p => p.PendingAmount) - _supplier.CreditBalance);
        ledger.ClosingBalance.Should().Be(1_335_000m - 1_130_000m);

        ledger.Rows.Should().NotContain(r => r.Id == anulada.Id);
        ledger.Rows.First().Source.Should().Be("legacy");
        ledger.Rows.First().Description.Should().Contain("COMP-0099");
        ledger.Rows.Select(r => r.Date).Should().BeInAscendingOrder();

        var op = ledger.Rows.Single(r => r.Code == "OP-0001");
        op.Source.Should().Be("payment_order");
        op.Credit.Should().Be(1_050_000m);
        op.Means.Should().HaveCount(4);
        var cheque = op.Means.First(m => m.Method == "check");
        cheque.ChequeNumber.Should().Be(chequeA.Numero);
        cheque.BankName.Should().Be("Banco Nación");
        cheque.DueDate.Should().Be(chequeA.FechaVencimiento);
        op.Means.Single(m => m.Method == "transfer").Reference.Should().Be("Ref. 88213");
        op.Imputations.Select(i => (i.Code, i.Amount)).Should().Equal(("COMP-0112", 685_000m), ("COMP-0118", 200_000m));

        ledger.Rows.Single(r => r.Id == oldPayment.Id).Source.Should().Be("payment");
        ledger.Rows.Single(r => r.Id == note.Id).Credit.Should().Be(20_000m);

        ledger.Pending.Should().ContainSingle();
        ledger.Pending[0].Code.Should().Be("COMP-0130");
        ledger.Pending[0].Pending.Should().Be(400_000m);
        ledger.Pending[0].AgeDays.Should().Be(0);
        pendiente.Status.Should().Be(PurchaseStatus.Active);

        // Con anulados: aparece la orden anulada, con el saldo que traía.
        var conAnulados = (await LedgerHandler([note]).Handle(
            new GetSupplierLedgerQuery(_supplier.Id, IncludeCancelled: true), CancellationToken.None)).Value;
        var rows = conAnulados.Rows.ToList();
        var idx = rows.FindIndex(r => r.Id == anulada.Id);
        rows[idx].IsCancelled.Should().BeTrue();
        rows[idx].Balance.Should().Be(rows[idx - 1].Balance);

        // Desde hace 10 días: el saldo anterior trae el pago en la compra y el pago sin orden.
        var desde = (await LedgerHandler([note]).Handle(
            new GetSupplierLedgerQuery(_supplier.Id, DateFrom: today.AddDays(-10)), CancellationToken.None)).Value;
        desde.OpeningBalance.Should().Be(-60_000m);
        desde.Rows.First().Id.Should().Be(note.Id);
        desde.ClosingBalance.Should().Be(ledger.ClosingBalance);
    }
}
