using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Advances.Commands.CancelPayrollAdvance;
using eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;
using eiti.Application.Features.Payroll.Advances.Queries.ListPayrollAdvances;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class PayrollAdvanceHandlersTests
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
    public async Task CreateHandler_ShouldPersistAdvance_WhenPaymentMethodIsTransfer()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Staff);
        var user = MockUser(companyId);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(employee.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var advanceRepository = new Mock<IPayrollAdvanceRepository>();
        PayrollAdvance? persisted = null;
        advanceRepository
            .Setup(r => r.AddAsync(It.IsAny<PayrollAdvance>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollAdvance, CancellationToken>((a, _) => persisted = a)
            .Returns(Task.CompletedTask);

        var handler = new CreatePayrollAdvanceHandler(
            user.Object,
            advanceRepository.Object,
            employeeRepository.Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreatePayrollAdvanceCommand(employee.Id.Value, 15000m, DateTime.UtcNow, "Adelanto", (int)PayrollPaymentMethod.Transfer, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Amount.Should().Be(15000m);
    }

    [Fact]
    public async Task CreateHandler_ShouldFail_WhenCashWithoutCashSessionId()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Staff);
        var user = MockUser(companyId);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(employee.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var handler = new CreatePayrollAdvanceHandler(
            user.Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            employeeRepository.Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreatePayrollAdvanceCommand(employee.Id.Value, 15000m, DateTime.UtcNow, null, (int)PayrollPaymentMethod.Cash, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CancelHandler_ShouldFail_WhenAdvanceNotPending()
    {
        var companyId = CompanyId.New();
        var advance = PayrollAdvance.Create(companyId, EmployeeId.New(), 10000m, DateTime.UtcNow, null, eiti.Domain.Users.UserId.New());
        advance.Cancel();
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollAdvanceRepository>();
        repository
            .Setup(r => r.GetByIdAsync(advance.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var handler = new CancelPayrollAdvanceHandler(user.Object, repository.Object, new Mock<ICashSessionRepository>().Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelPayrollAdvanceCommand(advance.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ListHandler_ShouldReturnMappedItems()
    {
        var companyId = CompanyId.New();
        var advance = PayrollAdvance.Create(companyId, EmployeeId.New(), 10000m, DateTime.UtcNow, null, eiti.Domain.Users.UserId.New());
        var user = MockUser(companyId);

        var repository = new Mock<IPayrollAdvanceRepository>();
        repository
            .Setup(r => r.ListByCompanyAsync(companyId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdvance> { advance });

        var handler = new ListPayrollAdvancesHandler(user.Object, repository.Object);

        var result = await handler.Handle(new ListPayrollAdvancesQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(x => x.Amount == 10000m);
    }
}
