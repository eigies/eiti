using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Suppliers.Commands.CancelSupplierPayment;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CancelSupplierPaymentHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReverseSupplierCreditGeneratedByCancelledOverpayment()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var userId = UserId.New();
        var drawer = CashDrawer.Create(companyId, branchId, "Caja 1");
        var session = CashSession.Open(companyId, branchId, drawer.Id, userId, 200_000m, null);
        var supplier = Supplier.Create(companyId.Value, "Proveedor", null, null, null, null);
        var payment = SupplierPayment.Create(
            companyId.Value,
            supplier.Id,
            branchId.Value,
            PurchasePaymentMethod.Cash,
            150_000m,
            DateTime.UtcNow,
            null,
            null,
            userId.Value);
        var purchase = Purchase.Create(
            companyId.Value,
            branchId.Value,
            supplier.Id,
            [PurchaseDetail.Create(Guid.NewGuid(), "Producto", 1, 100_000m)],
            null,
            null,
            userId.Value,
            "COM-001");

        purchase.AddPayment(PurchasePayment.Create(
            PurchasePaymentMethod.Cash,
            100_000m,
            DateTime.UtcNow,
            null,
            null,
            supplierPaymentId: payment.Id));
        supplier.AddCredit(50_000m);

        var currentUserService = new Mock<ICurrentUserService>();
        var supplierRepository = new Mock<ISupplierRepository>();
        var supplierPaymentRepository = new Mock<ISupplierPaymentRepository>();
        var purchaseRepository = new Mock<IPurchaseRepository>();
        var cashDrawerRepository = new Mock<ICashDrawerRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(x => x.CompanyId).Returns(companyId);
        currentUserService.SetupGet(x => x.UserId).Returns(userId);

        supplierPaymentRepository
            .Setup(x => x.GetByIdAsync(payment.Id, companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        supplierRepository
            .Setup(x => x.GetByIdAsync(supplier.Id, companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

        purchaseRepository
            .Setup(x => x.ListBySupplierPaymentIdAsync(companyId.Value, payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([purchase]);

        cashSessionRepository
            .Setup(x => x.GetBySupplierPaymentIdAsync(payment.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        cashDrawerRepository
            .Setup(x => x.GetByAssignedUserAsync(userId, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(drawer);

        cashSessionRepository
            .Setup(x => x.GetOpenByDrawerAsync(drawer.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new CancelSupplierPaymentHandler(
            currentUserService.Object,
            supplierRepository.Object,
            supplierPaymentRepository.Object,
            purchaseRepository.Object,
            cashDrawerRepository.Object,
            cashSessionRepository.Object,
            new Mock<IChequeRepository>().Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new CancelSupplierPaymentCommand(supplier.Id, payment.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        supplier.CreditBalance.Should().Be(0m);
        payment.Status.Should().Be(PurchasePaymentStatus.Cancelled);
        purchase.Payments.Single().Status.Should().Be(PurchasePaymentStatus.Cancelled);
        session.Movements.Should().ContainSingle(m =>
            m.Type == CashMovementType.PurchasePaymentCancellation
            && m.SupplierPaymentId == payment.Id
            && m.PaymentMethod == (int)PurchasePaymentMethod.Cash
            && m.Amount == 150_000m);
    }
}
