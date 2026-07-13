using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Liquidations.GetLiquidationById;
using eiti.Application.Features.Payroll.Liquidations.ListLiquidations;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class LiquidationQueriesTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        return user;
    }

    private static Mock<ICurrentUserService> MockUserWithoutCompany()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns((CompanyId?)null);
        return user;
    }

    [Fact]
    public async Task ListHandler_ShouldReturnPagedItems()
    {
        var companyId = CompanyId.New();
        var liquidation = PayrollLiquidation.Create(companyId, EmployeeId.New(), null, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), 500000m, [], [], []);
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollLiquidationRepository>();
        repository
            .Setup(r => r.ListAsync(companyId, null, null, null, 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollLiquidation> { liquidation });
        repository
            .Setup(r => r.CountAsync(companyId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new ListLiquidationsHandler(user.Object, repository.Object);

        var result = await handler.Handle(new ListLiquidationsQuery(null, null, null, 1, 25), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task ListHandler_ShouldFail_WhenCompanyIdMissing()
    {
        var user = MockUserWithoutCompany();
        var repository = new Mock<IPayrollLiquidationRepository>();

        var handler = new ListLiquidationsHandler(user.Object, repository.Object);

        var result = await handler.Handle(new ListLiquidationsQuery(null, null, null, 1, 25), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdHandler_ShouldFail_WhenNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollLiquidationRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollLiquidationId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollLiquidation?)null);

        var handler = new GetLiquidationByIdHandler(user.Object, repository.Object);

        var result = await handler.Handle(new GetLiquidationByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdHandler_ShouldFail_WhenCompanyIdMissing()
    {
        var user = MockUserWithoutCompany();
        var repository = new Mock<IPayrollLiquidationRepository>();

        var handler = new GetLiquidationByIdHandler(user.Object, repository.Object);

        var result = await handler.Handle(new GetLiquidationByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
