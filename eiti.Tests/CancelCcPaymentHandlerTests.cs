using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Commands.CancelCcPayment;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CancelCcPaymentHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReverseCustomerCreditGeneratedByCancelledOverpayment()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var drawer = CashDrawer.Create(companyId, branchId, "Caja 1");
        var userId = UserId.New();
        var customer = Customer.Create(companyId, "Daniel", "RVS", null);
        var product = Product.Create(companyId, "MAT-001", "MAT-001", "Contoso", "Material", null, 100_000m, 70_000m, null);
        var sale = Sale.CreateCc(
            companyId,
            branchId,
            customer.Id,
            [SaleDetail.Create(product.Id, 1, 100_000m)]);
        var payments = sale.AddCcPaymentGroup(
            [(SalePaymentMethod.Cash, 139_217m)],
            DateTime.UtcNow,
            null,
            allowOverpayment: true);
        customer.AddCredit(39_217m);

        var session = CashSession.Open(companyId, branchId, drawer.Id, userId, 200_000m, null);
        var stock = BranchProductStock.Create(companyId, branchId, product.Id);

        var currentUserService = new Mock<ICurrentUserService>();
        var saleRepository = new Mock<ISaleRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var cashDrawerRepository = new Mock<ICashDrawerRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(x => x.CompanyId).Returns(companyId);
        currentUserService.SetupGet(x => x.UserId).Returns(userId);

        saleRepository
            .Setup(x => x.GetByIdWithCcPaymentsAsync(sale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);

        branchProductStockRepository
            .Setup(x => x.GetOrCreateAsync(branchId, product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        cashDrawerRepository
            .Setup(x => x.GetByAssignedUserAsync(userId, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(drawer);

        cashSessionRepository
            .Setup(x => x.GetOpenByDrawerAsync(drawer.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        customerRepository
            .Setup(x => x.GetByIdAsync(customer.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        var handler = new CancelCcPaymentHandler(
            currentUserService.Object,
            saleRepository.Object,
            branchProductStockRepository.Object,
            stockMovementRepository.Object,
            cashDrawerRepository.Object,
            cashSessionRepository.Object,
            customerRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new CancelCcPaymentCommand(sale.Id.Value, payments[0].Id.Value),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        customer.CreditBalance.Should().Be(0m);
        sale.CcPaidTotal.Should().Be(0m);
        session.Movements.Should().ContainSingle(m =>
            m.Type == CashMovementType.CuentaCorrienteCancellation
            && m.PaymentMethod == (int)SalePaymentMethod.Cash
            && m.Amount == 139_217m);
    }
}
