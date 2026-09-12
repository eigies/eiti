using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Reports.Queries.CashSessionsReport;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Sales;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CashSessionsReportHandlerTests
{
    [Fact]
    public async Task Handle_ScopesToAllowedBranch_AndMapsOverlappingSession()
    {
        var companyId = CompanyId.New();
        var userId = UserId.New();
        var saleId = Guid.NewGuid();

        var allowedBranch = Branch.Create(companyId, "la sucunazi", null, null);
        var otherBranch = Branch.Create(companyId, "Sucursal Principal", null, null);
        var drawer = CashDrawer.Create(companyId, allowedBranch.Id, "Caja principal nazi");
        var otherDrawer = CashDrawer.Create(companyId, otherBranch.Id, "Caja principal");

        var session = CashSession.Open(companyId, allowedBranch.Id, drawer.Id, userId, 100_000m, null);
        session.RegisterSaleIncome(50_000m, saleId, userId);
        session.RegisterWithdrawal(10_000m, "Retiro de prueba", userId);

        var currentUserService = new Mock<ICurrentUserService>();
        var branchRepository = new Mock<IBranchRepository>();
        var cashDrawerRepository = new Mock<ICashDrawerRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var purchaseRepository = new Mock<IPurchaseRepository>();
        var userRepository = new Mock<IUserRepository>();

        currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(x => x.CompanyId).Returns(companyId);
        currentUserService.SetupGet(x => x.CanViewAllBranches).Returns(false);
        currentUserService.SetupGet(x => x.AllowedBranchIds).Returns(new[] { allowedBranch.Id.Value });

        branchRepository
            .Setup(x => x.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { allowedBranch, otherBranch });

        cashDrawerRepository
            .Setup(x => x.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { drawer, otherDrawer });

        IReadOnlyCollection<Guid>? forwardedBranchIds = null;
        cashSessionRepository
            .Setup(x => x.ListByCompanyAsync(
                companyId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<CompanyId, DateTime, DateTime, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, _, branchIds, _) => forwardedBranchIds = branchIds)
            .ReturnsAsync(new[] { session });

        saleRepository
            .Setup(x => x.GetPaymentsByCashSessionIdsAsync(It.IsAny<IEnumerable<CashSessionId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SalePayment>());
        saleRepository
            .Setup(x => x.GetCodesBySaleIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string?> { [saleId] = "SUCU-123-001" });
        userRepository
            .Setup(x => x.GetUsernamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [userId.Value] = "agustin testa" });

        var handler = new CashSessionsReportHandler(
            currentUserService.Object,
            branchRepository.Object,
            cashDrawerRepository.Object,
            cashSessionRepository.Object,
            saleRepository.Object,
            purchaseRepository.Object,
            userRepository.Object);

        var result = await handler.Handle(
            new CashSessionsReportQuery(new DateTime(2026, 5, 7), new DateTime(2026, 5, 7)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;

        // Scoping: solo la sucursal permitida aparece, aunque el repo devolvió las dos.
        response.Branches.Should().HaveCount(1);
        response.Branches[0].BranchName.Should().Be("la sucunazi");
        forwardedBranchIds.Should().BeEquivalentTo(new[] { allowedBranch.Id.Value });

        var drawerNode = response.Branches[0].Drawers.Should().ContainSingle().Subject;
        drawerNode.CashDrawerName.Should().Be("Caja principal nazi");

        var sessionNode = drawerNode.Sessions.Should().ContainSingle().Subject;
        sessionNode.OpeningAmount.Should().Be(100_000m);
        sessionNode.TotalsByType.Should().Contain(t => t.Type == (int)CashMovementType.SaleIncome && t.Count == 1 && t.Total == 50_000m);
        sessionNode.TotalsByType.Should().Contain(t => t.Type == (int)CashMovementType.OpeningFloat);

        // otherMovements excluye OpeningFloat/SaleIncome; incluye el retiro con su usuario.
        sessionNode.OtherMovements.Should().ContainSingle(m => m.Type == (int)CashMovementType.CashWithdrawal);
        sessionNode.OtherMovements.Should().OnlyContain(m => m.CreatedByUsername == "agustin testa");
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenRequestedBranchNotAllowed()
    {
        var companyId = CompanyId.New();
        var allowedBranchId = BranchId.New();
        var forbiddenBranchId = BranchId.New();

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(x => x.CompanyId).Returns(companyId);
        currentUserService.SetupGet(x => x.CanViewAllBranches).Returns(false);
        currentUserService.SetupGet(x => x.AllowedBranchIds).Returns(new[] { allowedBranchId.Value });

        var handler = new CashSessionsReportHandler(
            currentUserService.Object,
            new Mock<IBranchRepository>().Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<ISaleRepository>().Object,
            new Mock<IPurchaseRepository>().Object,
            new Mock<IUserRepository>().Object);

        var result = await handler.Handle(
            new CashSessionsReportQuery(new DateTime(2026, 5, 7), new DateTime(2026, 5, 7), forbiddenBranchId.Value),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Branches.Should().BeEmpty();
    }
}
