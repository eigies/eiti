using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Liquidations.PayLiquidation;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class PayLiquidationHandlerTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        user.SetupGet(u => u.UserId).Returns(eiti.Domain.Users.UserId.New());
        return user;
    }

    private static PayrollLiquidation CreateLiquidation(CompanyId companyId, decimal grossAmount = 500000m)
    {
        return PayrollLiquidation.Create(companyId, EmployeeId.New(), null, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), grossAmount, [], [], []);
    }

    [Fact]
    public async Task Handle_ShouldMarkAsPaid_WhenTransfer()
    {
        var companyId = CompanyId.New();
        var liquidation = CreateLiquidation(companyId);
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollLiquidationRepository>();
        repository
            .Setup(r => r.GetByIdAsync(liquidation.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(liquidation);

        var handler = new PayLiquidationHandler(
            user.Object,
            repository.Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new PayLiquidationCommand(liquidation.Id.Value, (int)PayrollPaymentMethod.Transfer, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be((int)PayrollLiquidationStatus.Paid);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLiquidationNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollLiquidationRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollLiquidationId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollLiquidation?)null);

        var handler = new PayLiquidationHandler(
            user.Object,
            repository.Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new PayLiquidationCommand(Guid.NewGuid(), (int)PayrollPaymentMethod.Transfer, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenCashSessionNotFound()
    {
        var companyId = CompanyId.New();
        var liquidation = CreateLiquidation(companyId);
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollLiquidationRepository>();
        repository
            .Setup(r => r.GetByIdAsync(liquidation.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(liquidation);

        var cashSessionRepository = new Mock<ICashSessionRepository>();
        cashSessionRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<eiti.Domain.Cash.CashSessionId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((eiti.Domain.Cash.CashSession?)null);

        var handler = new PayLiquidationHandler(
            user.Object,
            repository.Object,
            new Mock<ICashDrawerRepository>().Object,
            cashSessionRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new PayLiquidationCommand(liquidation.Id.Value, (int)PayrollPaymentMethod.Cash, Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
