using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Commands.CancelSale;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CancelSaleHandlerTests
{
    [Fact]
    public async Task Handle_ShouldRegisterCancellationInTheSaleOriginalCashSession()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var drawerA = CashDrawerId.New();
        var drawerB = CashDrawerId.New();
        var userId = UserId.New();
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria", null, 99000m, 70000m, null);
        var sale = Sale.Create(
            companyId,
            branchId,
            null,
            false,
            SaleStatus.OnHold,
            [SaleDetail.Create(product.Id, 1, product.Price)],
            [SalePayment.Create(SalePaymentMethod.Cash, 99000m, null)],
            allowOverpayment: true);

        var originalSession = CashSession.Open(companyId, branchId, drawerA, userId, 1000m, null);
        var otherSession = CashSession.Open(companyId, branchId, drawerB, userId, 2000m, null);

        sale.MarkAsPaid(drawerA, originalSession.Id);
        originalSession.RegisterSaleIncome(99000m, sale.Id.Value, userId);

        var stock = BranchProductStock.Create(companyId, branchId, product.Id);

        var currentUserService = new Mock<ICurrentUserService>();
        var saleRepository = new Mock<ISaleRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var cashDrawerRepository = new Mock<ICashDrawerRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var saleTransportAssignmentRepository = new Mock<ISaleTransportAssignmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(x => x.CompanyId).Returns(companyId);
        currentUserService.SetupGet(x => x.UserId).Returns(userId);

        saleRepository
            .Setup(x => x.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);

        branchProductStockRepository
            .Setup(x => x.GetOrCreateAsync(branchId, product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        cashSessionRepository
            .Setup(x => x.GetByIdAsync(originalSession.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalSession);

        var handler = new CancelSaleHandler(
            currentUserService.Object,
            saleRepository.Object,
            branchProductStockRepository.Object,
            stockMovementRepository.Object,
            cashDrawerRepository.Object,
            cashSessionRepository.Object,
            saleTransportAssignmentRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(new CancelSaleCommand(sale.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        originalSession.ExpectedClosingAmount.Should().Be(1000m);
        originalSession.Movements.Last().Type.Should().Be(CashMovementType.SaleCancellation);
        otherSession.ExpectedClosingAmount.Should().Be(2000m);
        otherSession.Movements.Should().OnlyContain(m => m.Type == CashMovementType.OpeningFloat);
        cashSessionRepository.Verify(x => x.GetAnyOpenByBranchAsync(
            It.IsAny<BranchId>(),
            It.IsAny<CompanyId>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
