using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.CashSessions.Queries.GetCurrentCashSession;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Sales;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class GetCurrentCashSessionHandlerTests
{
    [Fact]
    public async Task Handle_ShouldKeepSaleCodeForCancelledSaleMovements()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var drawerId = CashDrawerId.New();
        var userId = UserId.New();
        var saleId = Guid.NewGuid();
        var session = CashSession.Open(companyId, branchId, drawerId, userId, 0m, null);

        session.RegisterSaleIncome(99000m, saleId, userId);
        session.RegisterSaleCancellation(
            [SalePayment.Create(SalePaymentMethod.Cash, 99000m, null)],
            saleId,
            userId);

        var currentUserService = new Mock<ICurrentUserService>();
        var cashDrawerRepository = new Mock<ICashDrawerRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var userRepository = new Mock<IUserRepository>();

        currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(x => x.CompanyId).Returns(companyId);
        currentUserService.SetupGet(x => x.UserId).Returns(userId);
        currentUserService.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        cashSessionRepository
            .Setup(x => x.GetOpenByDrawerAsync(drawerId, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        saleRepository
            .Setup(x => x.GetPaymentsByCashSessionIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        saleRepository
            .Setup(x => x.GetCodesBySaleIdsAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Single() == saleId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string?> { [saleId] = "SUC-001" });

        userRepository
            .Setup(x => x.GetUsernamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [userId.Value] = "agustin" });

        var handler = new GetCurrentCashSessionHandler(
            currentUserService.Object,
            cashDrawerRepository.Object,
            cashSessionRepository.Object,
            saleRepository.Object,
            userRepository.Object);

        var result = await handler.Handle(new GetCurrentCashSessionQuery(drawerId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Movements.Should().OnlyContain(m => m.SaleCode == "SUC-001");
    }
}
