using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Liquidations.CancelLiquidation;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CancelLiquidationHandlerTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        user.SetupGet(u => u.UserId).Returns(eiti.Domain.Users.UserId.New());
        return user;
    }

    [Fact]
    public async Task Handle_ShouldCancelLiquidation_AndRevertAppliedAdvances_WhenPaidByTransfer()
    {
        var companyId = CompanyId.New();
        var employeeId = EmployeeId.New();
        var advance = PayrollAdvance.Create(companyId, employeeId, 20000m, DateTime.UtcNow, null, eiti.Domain.Users.UserId.New());
        var advanceLine = PayrollLiquidationAdvanceLine.Create(advance.Id.Value, 20000m);
        var liquidation = PayrollLiquidation.Create(companyId, employeeId, null, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), 500000m, [], [advanceLine], []);
        advance.Apply(liquidation.Id);
        liquidation.MarkAsPaid(PayrollPaymentMethod.Transfer, null);

        var user = MockUser(companyId);
        var liquidationRepository = new Mock<IPayrollLiquidationRepository>();
        liquidationRepository
            .Setup(r => r.GetByIdAsync(liquidation.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(liquidation);

        var advanceRepository = new Mock<IPayrollAdvanceRepository>();
        advanceRepository
            .Setup(r => r.GetByIdAsync(advance.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var handler = new CancelLiquidationHandler(
            user.Object,
            liquidationRepository.Object,
            advanceRepository.Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelLiquidationCommand(liquidation.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        liquidation.Status.Should().Be(PayrollLiquidationStatus.Cancelled);
        advance.Status.Should().Be(PayrollAdvanceStatus.Pending);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLiquidationNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);

        var liquidationRepository = new Mock<IPayrollLiquidationRepository>();
        liquidationRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollLiquidationId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollLiquidation?)null);

        var handler = new CancelLiquidationHandler(
            user.Object,
            liquidationRepository.Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelLiquidationCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenOriginalCashSessionIsClosed()
    {
        var companyId = CompanyId.New();
        var employeeId = EmployeeId.New();
        var liquidation = PayrollLiquidation.Create(companyId, employeeId, null, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), 500000m, [], [], []);

        var userId = eiti.Domain.Users.UserId.New();
        var session = CashSession.Open(companyId, BranchId.New(), CashDrawerId.New(), userId, 500000m, null);
        session.RegisterPayrollExpense(500000m, liquidation.Id.Value, userId);
        liquidation.MarkAsPaid(PayrollPaymentMethod.Cash, session.Id.Value);
        session.Close(session.ExpectedClosingAmount, userId, null);

        var user = MockUser(companyId);
        var liquidationRepository = new Mock<IPayrollLiquidationRepository>();
        liquidationRepository
            .Setup(r => r.GetByIdAsync(liquidation.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(liquidation);

        var cashSessionRepository = new Mock<ICashSessionRepository>();
        cashSessionRepository
            .Setup(r => r.GetByIdAsync(session.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new CancelLiquidationHandler(
            user.Object,
            liquidationRepository.Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            cashSessionRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelLiquidationCommand(liquidation.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        liquidation.Status.Should().Be(PayrollLiquidationStatus.Paid);
    }
}
