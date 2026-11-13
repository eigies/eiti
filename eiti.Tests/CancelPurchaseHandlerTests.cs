using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Suppliers.Common;
using eiti.Application.Features.Purchases.Commands.CancelPurchase;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Common;
using eiti.Domain.Companies;
using eiti.Domain.Purchases;
using eiti.Domain.Stock;
using eiti.Domain.Suppliers;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CancelPurchaseHandlerTests
{
    // Regresión del caso real: cancelar una compra con pagos imputados dejaba la plata "atrapada"
    // (imputación activa sobre una compra cancelada, sin volver como saldo a favor ni reintegro de caja).
    [Fact]
    public async Task Handle_ShouldRestoreCreditFromUntaggedAllocation_WhenReversingPayments()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var userId = UserId.New();
        var productId = Guid.NewGuid();

        var supplier = Supplier.Create(companyId.Value, "Proveedor", null, null, null, null);
        // Crédito preexistente (p.ej. de una cancelación anterior en modo "saldo a favor").
        supplier.AddCredit(150m);

        // La compra se crea con el proveedor YA con crédito: CreatePurchaseHandler auto-aplica ese saldo
        // (imputación SIN SupplierPaymentId, Method=SupplierCredit) -- este es el caso que rompía el fix
        // ingenuo ("cancelar sin restituir"). Simulamos ese estado directamente sobre el agregado.
        var purchase = Purchase.Create(
            companyId.Value, branchId.Value, supplier.Id,
            [PurchaseDetail.Create(productId, "Producto", 5, 100m)],
            null, null, userId.Value, "COMP-0001");

        purchase.AddPayment(PurchasePayment.Create(
            PurchasePaymentMethod.SupplierCredit, 150m, DateTime.UtcNow, null,
            "Saldo a favor aplicado automáticamente"));
        supplier.ConsumeCredit(150m);

        // Resto del total (350) financiado por un pago de proveedor real (transferencia).
        var payment = SupplierPayment.Create(
            companyId.Value, supplier.Id, branchId.Value,
            PurchasePaymentMethod.BankTransfer, 350m, DateTime.UtcNow, null, null, userId.Value);
        purchase.AddPayment(PurchasePayment.Create(
            PurchasePaymentMethod.SupplierCredit, 350m, DateTime.UtcNow, null, null,
            supplierPaymentId: payment.Id));

        purchase.PendingAmount.Should().Be(0m);
        supplier.CreditBalance.Should().Be(0m);

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(x => x.CompanyId).Returns(companyId);
        currentUserService.SetupGet(x => x.UserId).Returns(userId);
        currentUserService.SetupGet(x => x.AllowedBranchIds).Returns([branchId.Value]);
        currentUserService.SetupGet(x => x.CanViewAllBranches).Returns(true);

        var purchaseRepository = new Mock<IPurchaseRepository>();
        purchaseRepository
            .Setup(x => x.GetByIdAsync(purchase.Id, companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        purchaseRepository
            .Setup(x => x.ListBySupplierPaymentIdAsync(companyId.Value, payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([purchase]);

        var supplierRepository = new Mock<ISupplierRepository>();
        supplierRepository
            .Setup(x => x.GetByIdAsync(supplier.Id, companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

        var supplierPaymentRepository = new Mock<ISupplierPaymentRepository>();
        supplierPaymentRepository
            .Setup(x => x.GetByIdAsync(payment.Id, companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        // Stock previamente ingresado por la compra (lo que CreatePurchaseHandler habría sumado al crearla).
        var stock = BranchProductStock.Create(companyId, branchId, new eiti.Domain.Products.ProductId(productId));
        stock.ApplyManualEntry(5);

        var branchStockRepository = new Mock<IBranchProductStockRepository>();
        branchStockRepository
            .Setup(x => x.GetOrCreateAsync(branchId, It.IsAny<eiti.Domain.Products.ProductId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        var handler = new CancelPurchaseHandler(
            currentUserService.Object,
            purchaseRepository.Object,
            branchStockRepository.Object,
            new Mock<IStockMovementRepository>().Object,
            supplierRepository.Object,
            supplierPaymentRepository.Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new SupplierPaymentOrderService(
                supplierRepository.Object,
                supplierPaymentRepository.Object,
                new Mock<ISupplierPaymentOrderRepository>().Object,
                purchaseRepository.Object,
                new Mock<IChequeRepository>().Object,
                new Mock<IBankRepository>().Object,
                new Mock<ICompanyRepository>().Object),
            NoCreditNotes(),
            new Mock<IUnitOfWork>().Object);

        // Revertir el pago (ReversePayments): la porción $350 se deshace vía el pago real; la porción $150
        // (crédito consumido sin pago puntual) debe volver como saldo a favor, no perderse.
        var result = await handler.Handle(
            new CancelPurchaseCommand(purchase.Id, PurchaseCancellationRefundMode.ReversePayments),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        purchase.Status.Should().Be(PurchaseStatus.Cancelled);
        purchase.Payments.Should().OnlyContain(p => p.Status == PurchasePaymentStatus.Cancelled);
        supplier.CreditBalance.Should().Be(150m, "el crédito consumido sin pago puntual debe restituirse al cancelar, no perderse");
    }

    [Fact]
    public async Task Handle_ShouldRequireRefundMode_WhenPurchaseHasActivePayments()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var userId = UserId.New();
        var supplier = Supplier.Create(companyId.Value, "Proveedor", null, null, null, null);

        var purchase = Purchase.Create(
            companyId.Value, branchId.Value, supplier.Id,
            [PurchaseDetail.Create(Guid.NewGuid(), "Producto", 1, 100m)],
            null, null, userId.Value, "COMP-0002");
        purchase.AddPayment(PurchasePayment.Create(PurchasePaymentMethod.Cash, 100m, DateTime.UtcNow, null, null));

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(x => x.CompanyId).Returns(companyId);
        currentUserService.SetupGet(x => x.UserId).Returns(userId);

        var purchaseRepository = new Mock<IPurchaseRepository>();
        purchaseRepository
            .Setup(x => x.GetByIdAsync(purchase.Id, companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);

        var handler = new CancelPurchaseHandler(
            currentUserService.Object,
            purchaseRepository.Object,
            new Mock<IBranchProductStockRepository>().Object,
            new Mock<IStockMovementRepository>().Object,
            new Mock<ISupplierRepository>().Object,
            new Mock<ISupplierPaymentRepository>().Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<ISupplierPaymentOrderService>().Object,
            NoCreditNotes(),
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelPurchaseCommand(purchase.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Purchases.Cancel.RefundModeRequired");
    }

    // Espejo de CancelSale (NCC-003 de LACTEOS BARRAZA): con una NC vigente sobre la compra, anularla deja
    // crédito a favor por mercadería que ya no figura comprada. Primero se anula la NC.
    [Fact]
    public async Task Handle_ShouldReject_WhenPurchaseHasActiveCreditNote()
    {
        var ctx = new PurchaseCancelContext();
        ctx.LinkCreditNote("NCP-004");

        var result = await ctx.Handler.Handle(new CancelPurchaseCommand(ctx.Purchase.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Purchases.Cancel.HasActiveCreditNote");
        result.Error.Description.Should().Contain("NCP-004");
        ctx.Purchase.Status.Should().NotBe(PurchaseStatus.Cancelled);
        ctx.Stock.OnHandQuantity.Should().Be(5);
        ctx.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCancel_WhenLinkedCreditNoteIsCancelled()
    {
        var ctx = new PurchaseCancelContext();
        ctx.LinkCreditNote("NCP-003").Cancel(ctx.UserId.Value);

        var result = await ctx.Handler.Handle(new CancelPurchaseCommand(ctx.Purchase.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        ctx.Purchase.Status.Should().Be(PurchaseStatus.Cancelled);
        ctx.Stock.OnHandQuantity.Should().Be(0);
    }

    // ── Anular la compra junto con sus notas de crédito vigentes ────────────────
    // Caso real (BATERIAS MATEO, Mataderos): COMP-0218 nació Pagada porque el saldo a favor se aplicó solo al
    // crearla (sin back-link). El botón de anular no aparecía en compras pagadas, así que el usuario la "anuló" con
    // NCP-002 por el total; sin compras pendientes, ese crédito quedó a favor y COMP-0219 lo consumió al crearse.
    // Desde ahí no había salida: la compra no se anulaba por la NC vigente y la NC no, por crédito ya consumido.
    // Juntas cierran en cero: la compra devuelve al saldo lo mismo que la nota le saca.

    [Fact]
    public async Task Handle_CasoMateo_AnulaLaCompraYSuNotaDeCreditoJuntas()
    {
        var (ctx, note, comp0219) = MateoCase();

        var result = await ctx.Handler.Handle(
            new CancelPurchaseCommand(ctx.Purchase.Id, PurchaseCancellationRefundMode.Credit, CancelLinkedCreditNotes: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        note.Status.Should().Be(CreditNoteStatus.Cancelled);
        ctx.Purchase.Status.Should().Be(PurchaseStatus.Cancelled);
        ctx.Purchase.Payments.Should().OnlyContain(p => p.Status == PurchasePaymentStatus.Cancelled);
        ctx.Stock.OnHandQuantity.Should().Be(0);
        ctx.StockMovements.Verify(x => x.AddAsync(
            It.Is<StockMovement>(m => m.Type == StockMovementType.PurchaseReturn && m.Quantity == 2),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Supplier.CreditBalance.Should().Be(0m);
        comp0219.Status.Should().Be(PurchaseStatus.Active);
        comp0219.Payments.Should().ContainSingle()
            .Which.Should().Match<PurchasePayment>(p => p.Status == PurchasePaymentStatus.Active && p.Amount == 231_890.86m);
        ctx.Session.Movements.Should().ContainSingle(m => m.Type == CashMovementType.SupplierCreditNoteCancellation);
        ctx.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // Sin la confirmación del usuario se rechaza como antes, pero el mensaje dice qué nota se anularía (y por
    // cuánto) para que el front lo muestre y pregunte.
    [Fact]
    public async Task Handle_CasoMateo_SinConfirmar_RechazaYNoTocaNada()
    {
        var (ctx, note, comp0219) = MateoCase();

        var result = await ctx.Handler.Handle(
            new CancelPurchaseCommand(ctx.Purchase.Id, PurchaseCancellationRefundMode.Credit), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Purchases.Cancel.HasActiveCreditNote");
        result.Error.Description.Should().Contain("NCP-002").And.Contain("231.890,86").And.Contain("Si confirmás");
        ctx.ShouldBeUntouched(note, PurchaseStatus.Paid, stock: 2);
        comp0219.Payments.Should().OnlyContain(p => p.Status == PurchasePaymentStatus.Active);
    }

    // La anulación de la nota queda en la caja (movimiento neutro), igual que anularla sola: sin caja abierta no
    // se anula nada.
    [Fact]
    public async Task Handle_ConNotaYSinCajaAbierta_RechazaYNoTocaNada()
    {
        var (ctx, note, _) = MateoCase(withOpenSession: false);

        var result = await ctx.Handler.Handle(
            new CancelPurchaseCommand(ctx.Purchase.Id, PurchaseCancellationRefundMode.Credit, CancelLinkedCreditNotes: true),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Purchases.Cancel.CreditNoteNoCashSessionOpen");
        ctx.ShouldBeUntouched(note, PurchaseStatus.Paid, stock: 2);
    }

    // La nota imputó sobre la misma compra que se anula. Esa imputación la deshace la anulación de la nota; si
    // además se devolviera como "lo pagado" de la compra, los mismos 150 volverían dos veces al saldo.
    [Fact]
    public async Task Handle_NotaImputadaEnLaMismaCompra_NoSeDevuelveDosVeces()
    {
        var ctx = new PurchaseCancelContext();
        ctx.Purchase.AddPayment(PurchasePayment.Create(PurchasePaymentMethod.Cash, 200m, DateTime.UtcNow, null, null));
        var note = ctx.LinkCreditNote("NCP-005", 150m);
        ctx.ApplyCredit(ctx.Purchase, 150m, creditNoteId: note.Id);

        var result = await ctx.Handler.Handle(
            new CancelPurchaseCommand(ctx.Purchase.Id, PurchaseCancellationRefundMode.Credit, CancelLinkedCreditNotes: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        note.Status.Should().Be(CreditNoteStatus.Cancelled);
        ctx.Purchase.Status.Should().Be(PurchaseStatus.Cancelled);
        ctx.Purchase.Payments.Should().OnlyContain(p => p.Status == PurchasePaymentStatus.Cancelled);
        // Vuelven los 200 pagados; los 150 de la nota entran con su imputación y salen con la nota.
        ctx.Supplier.CreditBalance.Should().Be(200m);
    }

    // El mismo cruce del lado del chequeo: NCP-006 por 300 imputó 250 en la compra y los 50 que sobraron los
    // consumió otra compra sin back-link. Anular las dos deja el saldo en −50; contando dos veces los 250 (como
    // imputación de la nota y como pago de la compra) parecería que sobran 200.
    [Fact]
    public async Task Handle_NotaImputadaEnLaMismaCompra_ElChequeoDeSaldoTampocoLaCuentaDosVeces()
    {
        var ctx = new PurchaseCancelContext(quantity: 1, unitCost: 250m);
        var note = ctx.LinkCreditNote("NCP-006", 300m);
        ctx.ApplyCredit(ctx.Purchase, 250m, creditNoteId: note.Id);
        var other = ctx.AddPurchase("COMP-0010", 80m);
        ctx.ApplyCredit(other, 50m);

        var result = await ctx.Handler.Handle(
            new CancelPurchaseCommand(ctx.Purchase.Id, PurchaseCancellationRefundMode.Credit, CancelLinkedCreditNotes: true),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Purchases.Cancel.InsufficientCreditForCreditNotes");
        ctx.ShouldBeUntouched(note, PurchaseStatus.Paid, stock: 1);
        other.Payments.Should().OnlyContain(p => p.Status == PurchasePaymentStatus.Active);
    }

    // NCP-007 sobre una compra pendiente: su crédito fue a saldo y lo consumió otra compra. La compra no tiene pagos
    // que devolver, así que anular la nota dejaría el saldo en −100: se rechaza antes de tocar nada.
    [Fact]
    public async Task Handle_SaldoCombinadoInsuficiente_RechazaYNoTocaNada()
    {
        var ctx = new PurchaseCancelContext();
        var note = ctx.LinkCreditNote("NCP-007", 100m);
        var other = ctx.AddPurchase("COMP-0011", 300m);
        ctx.ApplyCredit(other, 100m);

        var result = await ctx.Handler.Handle(
            new CancelPurchaseCommand(ctx.Purchase.Id, CancelLinkedCreditNotes: true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Purchases.Cancel.InsufficientCreditForCreditNotes");
        result.Error.Description.Should().Contain("NCP-007").And.Contain("100,00");
        ctx.ShouldBeUntouched(note, PurchaseStatus.Active, stock: 5);
        other.Payments.Should().OnlyContain(p => p.Status == PurchasePaymentStatus.Active);
    }

    // "Revertir el pago" con la nota: el pago se revierte entero (reintegra la caja) y la nota se anula; cada
    // imputación en la compra la deshace su propio camino.
    [Fact]
    public async Task Handle_RevertirElPagoYAnularLaNota()
    {
        var ctx = new PurchaseCancelContext();
        var payment = ctx.AddSupplierPayment(PurchasePaymentMethod.Cash, 300m);
        ctx.ApplyCredit(ctx.Purchase, 300m, supplierPaymentId: payment.Id);
        var note = ctx.LinkCreditNote("NCP-008", 200m);
        ctx.ApplyCredit(ctx.Purchase, 200m, creditNoteId: note.Id);

        var result = await ctx.Handler.Handle(
            new CancelPurchaseCommand(ctx.Purchase.Id, PurchaseCancellationRefundMode.ReversePayments, CancelLinkedCreditNotes: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        payment.Status.Should().Be(PurchasePaymentStatus.Cancelled);
        note.Status.Should().Be(CreditNoteStatus.Cancelled);
        ctx.Purchase.Status.Should().Be(PurchaseStatus.Cancelled);
        ctx.Purchase.Payments.Should().OnlyContain(p => p.Status == PurchasePaymentStatus.Cancelled);
        ctx.Supplier.CreditBalance.Should().Be(0m);
        ctx.Session.Movements.Should().ContainSingle(m =>
            m.Type == CashMovementType.PurchasePaymentCancellation && m.Direction == CashMovementDirection.In && m.Amount == 300m);
    }

    // La nota había saldado otra compra: al anularla, esa compra vuelve a pendiente, y el saldo a favor que deja la
    // anulación se le imputa FIFO, igual que en el modo "Saldo a favor".
    [Fact]
    public async Task Handle_LoQueQuedaAFavorSeImputaFifo_TambienALaCompraQueLaNotaReabrio()
    {
        var ctx = new PurchaseCancelContext();
        ctx.Purchase.AddPayment(PurchasePayment.Create(PurchasePaymentMethod.Cash, 500m, DateTime.UtcNow, null, null));
        var older = ctx.AddPurchase("COMP-0001", 120m);
        var note = ctx.LinkCreditNote("NCP-009", 120m);
        ctx.ApplyCredit(older, 120m, creditNoteId: note.Id);
        older.Status.Should().Be(PurchaseStatus.Paid);

        var result = await ctx.Handler.Handle(
            new CancelPurchaseCommand(ctx.Purchase.Id, PurchaseCancellationRefundMode.Credit, CancelLinkedCreditNotes: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        note.Status.Should().Be(CreditNoteStatus.Cancelled);
        older.Status.Should().Be(PurchaseStatus.Paid);
        older.Payments.Where(p => p.Status == PurchasePaymentStatus.Active).Should().ContainSingle()
            .Which.CreditNoteId.Should().BeNull();
        ctx.Supplier.CreditBalance.Should().Be(380m);
    }

    // COMP-0218 (2 × 115.945,43) pagada entera con saldo a favor al crearse, sin back-link; NCP-002 por el total sobre
    // ella; su crédito quedó a favor y COMP-0219 lo consumió al crearse, también sin back-link. Saldo a favor: 0.
    private static (PurchaseCancelContext Ctx, SupplierCreditNote Note, Purchase Comp0219) MateoCase(bool withOpenSession = true)
    {
        var ctx = new PurchaseCancelContext(quantity: 2, unitCost: 115_945.43m, code: "COMP-0218", withOpenSession: withOpenSession);
        ctx.Supplier.AddCredit(231_890.86m); // pagos por adelantado
        ctx.ApplyCredit(ctx.Purchase, 231_890.86m);

        var note = ctx.LinkCreditNote("NCP-002", 231_890.86m);

        var comp0219 = ctx.AddPurchase("COMP-0219", 400_000m);
        ctx.ApplyCredit(comp0219, 231_890.86m);

        ctx.Purchase.Status.Should().Be(PurchaseStatus.Paid);
        ctx.Supplier.CreditBalance.Should().Be(0m);
        return (ctx, note, comp0219);
    }

    private static ISupplierCreditNoteRepository NoCreditNotes()
    {
        var repository = new Mock<ISupplierCreditNoteRepository>();
        repository
            .Setup(x => x.ListByPurchaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return repository.Object;
    }

    // Compra sin pagos (por defecto 5 unidades a 100) con su stock ya ingresado, de un proveedor sin saldo a favor.
    // Los repositorios responden como los reales sobre las compras que conoce el contexto: las de una nota o un pago
    // por su back-link, y las pendientes según su estado en memoria.
    private sealed class PurchaseCancelContext
    {
        public CompanyId CompanyId { get; } = CompanyId.New();
        public BranchId BranchId { get; } = BranchId.New();
        public UserId UserId { get; } = UserId.New();
        public Supplier Supplier { get; }
        public Purchase Purchase { get; }
        public BranchProductStock Stock { get; }
        public CashSession Session { get; }
        public Mock<IStockMovementRepository> StockMovements { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public CancelPurchaseHandler Handler { get; }

        private readonly List<SupplierCreditNote> _creditNotes = [];
        private readonly List<Purchase> _purchases = [];
        private readonly List<SupplierPayment> _payments = [];

        public PurchaseCancelContext(
            int quantity = 5, decimal unitCost = 100m, string code = "COMP-0003", bool withOpenSession = true)
        {
            Supplier = Supplier.Create(CompanyId.Value, "Proveedor", null, null, null, null);
            var productId = Guid.NewGuid();
            Purchase = Purchase.Create(
                CompanyId.Value, BranchId.Value, Supplier.Id,
                [PurchaseDetail.Create(productId, "Producto", quantity, unitCost)],
                null, null, UserId.Value, code);
            _purchases.Add(Purchase);

            Stock = BranchProductStock.Create(CompanyId, BranchId, new eiti.Domain.Products.ProductId(productId));
            Stock.ApplyManualEntry(quantity);

            var drawer = CashDrawer.Create(CompanyId, BranchId, "Caja 1");
            Session = CashSession.Open(CompanyId, BranchId, drawer.Id, UserId, 0m, null);

            var currentUserService = new Mock<ICurrentUserService>();
            currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
            currentUserService.SetupGet(x => x.CompanyId).Returns(CompanyId);
            currentUserService.SetupGet(x => x.UserId).Returns(UserId);

            var purchaseRepository = new Mock<IPurchaseRepository>();
            purchaseRepository
                .Setup(x => x.GetByIdAsync(Purchase.Id, CompanyId.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Purchase);
            purchaseRepository
                .Setup(x => x.ListByCreditNoteIdAsync(CompanyId.Value, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid noteId, CancellationToken _) =>
                    _purchases.Where(p => p.Payments.Any(pay => pay.CreditNoteId == noteId)).ToList());
            purchaseRepository
                .Setup(x => x.ListBySupplierPaymentIdAsync(CompanyId.Value, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid paymentId, CancellationToken _) =>
                    _purchases.Where(p => p.Payments.Any(pay => pay.SupplierPaymentId == paymentId)).ToList());
            purchaseRepository
                .Setup(x => x.ListPendingBySupplierAsync(CompanyId.Value, Supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _purchases.Where(p => p.Status == PurchaseStatus.Active).ToList());

            var stockRepository = new Mock<IBranchProductStockRepository>();
            stockRepository
                .Setup(x => x.GetOrCreateAsync(BranchId, It.IsAny<eiti.Domain.Products.ProductId>(), CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Stock);

            var supplierRepository = new Mock<ISupplierRepository>();
            supplierRepository
                .Setup(x => x.GetByIdAsync(Supplier.Id, CompanyId.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Supplier);

            var supplierPaymentRepository = new Mock<ISupplierPaymentRepository>();
            supplierPaymentRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), CompanyId.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, Guid _, CancellationToken _) => _payments.SingleOrDefault(p => p.Id == id));

            var drawerRepository = new Mock<ICashDrawerRepository>();
            drawerRepository
                .Setup(x => x.GetByAssignedUserAsync(UserId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(drawer);
            var sessionRepository = new Mock<ICashSessionRepository>();
            sessionRepository
                .Setup(x => x.GetOpenByDrawerAsync(drawer.Id, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(withOpenSession ? Session : null);

            var creditNoteRepository = new Mock<ISupplierCreditNoteRepository>();
            creditNoteRepository
                .Setup(x => x.ListByPurchaseAsync(CompanyId.Value, Purchase.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _creditNotes.ToList());

            Handler = new CancelPurchaseHandler(
                currentUserService.Object,
                purchaseRepository.Object,
                stockRepository.Object,
                StockMovements.Object,
                supplierRepository.Object,
                supplierPaymentRepository.Object,
                drawerRepository.Object,
                sessionRepository.Object,
                new SupplierPaymentOrderService(
                    supplierRepository.Object,
                    supplierPaymentRepository.Object,
                    new Mock<ISupplierPaymentOrderRepository>().Object,
                    purchaseRepository.Object,
                    new Mock<IChequeRepository>().Object,
                    new Mock<IBankRepository>().Object,
                    new Mock<ICompanyRepository>().Object),
                creditNoteRepository.Object,
                UnitOfWork.Object);
        }

        // Nota de crédito emitida sobre la compra (PurchaseId), como la deja CreateSupplierCreditNote: su importe
        // entra al saldo a favor, y lo que imputa cada test con ApplyCredit sale de ahí.
        public SupplierCreditNote LinkCreditNote(string code, decimal amount = 100m)
        {
            var note = SupplierCreditNote.Create(
                CompanyId.Value, Supplier.Id, BranchId.Value, code, amount,
                "devolución de una unidad", DateTime.UtcNow, Purchase.Id, UserId.Value);
            _creditNotes.Add(note);
            Supplier.AddCredit(amount);
            return note;
        }

        // Otra compra del mismo proveedor, pendiente y sin pagos.
        public Purchase AddPurchase(string code, decimal total)
        {
            var purchase = Purchase.Create(
                CompanyId.Value, BranchId.Value, Supplier.Id,
                [PurchaseDetail.Create(Guid.NewGuid(), "Producto", 1, total)],
                null, null, UserId.Value, code);
            _purchases.Add(purchase);
            return purchase;
        }

        // Pago de proveedor ya hecho: su plata entra al saldo a favor hasta que se imputa.
        public SupplierPayment AddSupplierPayment(PurchasePaymentMethod method, decimal amount)
        {
            var payment = SupplierPayment.Create(
                CompanyId.Value, Supplier.Id, BranchId.Value, method, amount, DateTime.UtcNow, null, null, UserId.Value);
            _payments.Add(payment);
            Supplier.AddCredit(amount);
            return payment;
        }

        // Imputa saldo a favor a una compra como lo hace SupplierCreditApplicator, con el back-link que corresponda
        // (ninguno = saldo a favor aplicado al crear la compra o por otra anulación).
        public void ApplyCredit(Purchase purchase, decimal amount, Guid? creditNoteId = null, Guid? supplierPaymentId = null)
        {
            Supplier.ConsumeCredit(amount);
            purchase.AddPayment(PurchasePayment.Create(
                PurchasePaymentMethod.SupplierCredit, amount, DateTime.UtcNow, null, "Saldo a favor aplicado automáticamente",
                supplierPaymentId: supplierPaymentId, creditNoteId: creditNoteId));
        }

        public void ShouldBeUntouched(SupplierCreditNote note, PurchaseStatus status, int stock)
        {
            note.Status.Should().Be(CreditNoteStatus.Active);
            Purchase.Status.Should().Be(status);
            Purchase.Payments.Should().NotContain(p => p.Status != PurchasePaymentStatus.Active);
            Stock.OnHandQuantity.Should().Be(stock);
            Supplier.CreditBalance.Should().Be(0m);
            Session.Movements.Should().NotContain(m => m.Type == CashMovementType.SupplierCreditNoteCancellation);
            UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
