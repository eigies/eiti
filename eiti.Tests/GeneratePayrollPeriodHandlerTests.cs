using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class GeneratePayrollPeriodHandlerTests
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
    public async Task Handle_ShouldGenerateLiquidation_ForEligibleEmployee_WithDeductionsAndAdvancesApplied()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Staff);
        employee.SetPayrollConfig(500000m, PayrollPeriodicity.Monthly);

        var user = MockUser(companyId);
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { employee });

        var concept = PayrollDeductionConcept.Create(companyId, "Jubilacion", 11m);
        var deductionRepository = new Mock<IPayrollDeductionConceptRepository>();
        deductionRepository
            .Setup(r => r.ListByCompanyAsync(companyId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollDeductionConcept> { concept });

        var advance = PayrollAdvance.Create(companyId, employee.Id, 20000m, DateTime.UtcNow, null, eiti.Domain.Users.UserId.New());
        var advanceRepository = new Mock<IPayrollAdvanceRepository>();
        advanceRepository
            .Setup(r => r.ListPendingByEmployeeAsync(companyId, employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdvance> { advance });

        var liquidationRepository = new Mock<IPayrollLiquidationRepository>();
        liquidationRepository
            .Setup(r => r.ExistsForPeriodAsync(companyId, employee.Id, "2026-07", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        PayrollLiquidation? persisted = null;
        liquidationRepository
            .Setup(r => r.AddAsync(It.IsAny<PayrollLiquidation>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollLiquidation, CancellationToken>((l, _) => persisted = l)
            .Returns(Task.CompletedTask);

        var handler = new GeneratePayrollPeriodHandler(
            user.Object,
            employeeRepository.Object,
            deductionRepository.Object,
            advanceRepository.Object,
            liquidationRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new GeneratePayrollPeriodCommand((int)PayrollPeriodicity.Monthly, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.GeneratedCount.Should().Be(1);
        persisted.Should().NotBeNull();
        persisted!.NetAmount.Should().Be(425000m); // 500000 - 55000 (11%) - 20000 (adelanto)
        advance.Status.Should().Be(PayrollAdvanceStatus.Applied);
    }

    [Fact]
    public async Task Handle_ShouldSkip_WhenEmployeeHasNoBaseSalary()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Staff);
        var user = MockUser(companyId);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { employee });

        var handler = new GeneratePayrollPeriodHandler(
            user.Object,
            employeeRepository.Object,
            new Mock<IPayrollDeductionConceptRepository>().Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            new Mock<IPayrollLiquidationRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new GeneratePayrollPeriodCommand((int)PayrollPeriodicity.Monthly, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.GeneratedCount.Should().Be(0);
        result.Value.Skipped.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldSkip_WhenLiquidationAlreadyExistsForPeriod()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Staff);
        employee.SetPayrollConfig(500000m, PayrollPeriodicity.Monthly);
        var user = MockUser(companyId);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { employee });

        var liquidationRepository = new Mock<IPayrollLiquidationRepository>();
        liquidationRepository
            .Setup(r => r.ExistsForPeriodAsync(companyId, employee.Id, "2026-07", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new GeneratePayrollPeriodHandler(
            user.Object,
            employeeRepository.Object,
            new Mock<IPayrollDeductionConceptRepository>().Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            liquidationRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new GeneratePayrollPeriodCommand((int)PayrollPeriodicity.Monthly, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)),
            CancellationToken.None);

        result.Value.GeneratedCount.Should().Be(0);
        result.Value.Skipped.Single().Reason.Should().Contain("período");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenCompanyIdIsNull()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns((CompanyId?)null);
        user.SetupGet(u => u.UserId).Returns(eiti.Domain.Users.UserId.New());

        var handler = new GeneratePayrollPeriodHandler(
            user.Object,
            new Mock<IEmployeeRepository>().Object,
            new Mock<IPayrollDeductionConceptRepository>().Object,
            new Mock<IPayrollAdvanceRepository>().Object,
            new Mock<IPayrollLiquidationRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new GeneratePayrollPeriodCommand((int)PayrollPeriodicity.Monthly, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
