using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class SetEmployeePayrollConfigHandlerTests
{
    [Fact]
    public async Task Handle_ShouldSetBaseSalaryAndPeriodicity_WhenEmployeeExists()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Staff);

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);

        var repository = new Mock<IEmployeeRepository>();
        repository
            .Setup(r => r.GetByIdAsync(employee.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var handler = new SetEmployeePayrollConfigHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new SetEmployeePayrollConfigCommand(employee.Id.Value, 500000m, (int)PayrollPeriodicity.Monthly),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BaseSalary.Should().Be(500000m);
        result.Value.PayrollPeriodicity.Should().Be((int)PayrollPeriodicity.Monthly);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEmployeeNotFound()
    {
        var companyId = CompanyId.New();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);

        var repository = new Mock<IEmployeeRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<EmployeeId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var handler = new SetEmployeePayrollConfigHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new SetEmployeePayrollConfigCommand(Guid.NewGuid(), 500000m, (int)PayrollPeriodicity.Monthly),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPeriodicityInvalid()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Staff);

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);

        var repository = new Mock<IEmployeeRepository>();
        repository
            .Setup(r => r.GetByIdAsync(employee.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var handler = new SetEmployeePayrollConfigHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new SetEmployeePayrollConfigCommand(employee.Id.Value, 500000m, 999),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
