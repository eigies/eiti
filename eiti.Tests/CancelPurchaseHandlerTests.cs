using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Suppliers.Common;
using eiti.Application.Features.Purchases.Commands.CancelPurchase;
using eiti.Domain.Branches;
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
                new Mock<IChequeRepository>().Object),
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

    private static ISupplierCreditNoteRepository NoCreditNotes()
    {
        var repository = new Mock<ISupplierCreditNoteRepository>();
        repository
            .Setup(x => x.ListByPurchaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return repository.Object;
    }

    // Compra sin pagos de 5 unidades, con su stock ya ingresado.
    private sealed class PurchaseCancelContext
    {
        public CompanyId CompanyId { get; } = CompanyId.New();
        public BranchId BranchId { get; } = BranchId.New();
        public UserId UserId { get; } = UserId.New();
        public Supplier Supplier { get; }
        public Purchase Purchase { get; }
        public BranchProductStock Stock { get; }
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public CancelPurchaseHandler Handler { get; }

        private readonly List<SupplierCreditNote> _creditNotes = [];

        public PurchaseCancelContext()
        {
            Supplier = Supplier.Create(CompanyId.Value, "Proveedor", null, null, null, null);
            var productId = Guid.NewGuid();
            Purchase = Purchase.Create(
                CompanyId.Value, BranchId.Value, Supplier.Id,
                [PurchaseDetail.Create(productId, "Producto", 5, 100m)],
                null, null, UserId.Value, "COMP-0003");

            Stock = BranchProductStock.Create(CompanyId, BranchId, new eiti.Domain.Products.ProductId(productId));
            Stock.ApplyManualEntry(5);

            var currentUserService = new Mock<ICurrentUserService>();
            currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
            currentUserService.SetupGet(x => x.CompanyId).Returns(CompanyId);
            currentUserService.SetupGet(x => x.UserId).Returns(UserId);

            var purchaseRepository = new Mock<IPurchaseRepository>();
            purchaseRepository
                .Setup(x => x.GetByIdAsync(Purchase.Id, CompanyId.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Purchase);

            var stockRepository = new Mock<IBranchProductStockRepository>();
            stockRepository
                .Setup(x => x.GetOrCreateAsync(BranchId, It.IsAny<eiti.Domain.Products.ProductId>(), CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Stock);

            var creditNoteRepository = new Mock<ISupplierCreditNoteRepository>();
            creditNoteRepository
                .Setup(x => x.ListByPurchaseAsync(CompanyId.Value, Purchase.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _creditNotes.ToList());

            Handler = new CancelPurchaseHandler(
                currentUserService.Object,
                purchaseRepository.Object,
                stockRepository.Object,
                new Mock<IStockMovementRepository>().Object,
                new Mock<ISupplierRepository>().Object,
                new Mock<ISupplierPaymentRepository>().Object,
                new Mock<ICashDrawerRepository>().Object,
                new Mock<ICashSessionRepository>().Object,
                new Mock<ISupplierPaymentOrderService>().Object,
                creditNoteRepository.Object,
                UnitOfWork.Object);
        }

        // Nota de crédito emitida sobre la compra (PurchaseId), como la deja CreateSupplierCreditNote.
        public SupplierCreditNote LinkCreditNote(string code)
        {
            var note = SupplierCreditNote.Create(
                CompanyId.Value, Supplier.Id, BranchId.Value, code, 100m,
                "devolución de una unidad", DateTime.UtcNow, Purchase.Id, UserId.Value);
            _creditNotes.Add(note);
            return note;
        }
    }
}
