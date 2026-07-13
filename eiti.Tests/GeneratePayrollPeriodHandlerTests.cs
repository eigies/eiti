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
            new Mock<IPayrollBonusRepository>().Object,
            new Mock<IPayrollBonusConceptRepository>().Object,
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
            new Mock<IPayrollBonusRepository>().Object,
            new Mock<IPayrollBonusConceptRepository>().Object,
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
            new Mock<IPayrollBonusRepository>().Object,
            new Mock<IPayrollBonusConceptRepository>().Object,
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
            new Mock<IPayrollBonusRepository>().Object,
            new Mock<IPayrollBonusConceptRepository>().Object,
            new Mock<IPayrollLiquidationRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new GeneratePayrollPeriodCommand((int)PayrollPeriodicity.Monthly, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldAddBonusLines_WhenEmployeeHasPendingBonuses_FixedAndPercentage()
    {
        var companyId = CompanyId.New();
        var employee = Employee.Create(companyId, null, "Ana", "Lopez", null, null, null, EmployeeRole.Staff);
        employee.SetPayrollConfig(300000m, PayrollPeriodicity.Monthly);

        var user = MockUser(companyId);
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { employee });

        var deductionConceptRepository = new Mock<IPayrollDeductionConceptRepository>();
        deductionConceptRepository
            .Setup(r => r.ListByCompanyAsync(companyId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollDeductionConcept>());

        var advanceRepository = new Mock<IPayrollAdvanceRepository>();
        advanceRepository
            .Setup(r => r.ListPendingByEmployeeAsync(companyId, employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdvance>());

        var concept = PayrollBonusConcept.Create(companyId, "Presentismo");
        var bonusConceptRepository = new Mock<IPayrollBonusConceptRepository>();
        bonusConceptRepository
            .Setup(r => r.GetByIdAsync(concept.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(concept);

        var fixedBonus = PayrollBonus.Create(companyId, employee.Id, concept.Id, PayrollBonusAmountType.FixedAmount, 15000m, null);
        var percentageBonus = PayrollBonus.Create(companyId, employee.Id, concept.Id, PayrollBonusAmountType.Percentage, 10m, null);
        var bonusRepository = new Mock<IPayrollBonusRepository>();
        bonusRepository
            .Setup(r => r.ListPendingByEmployeeAsync(companyId, employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollBonus> { fixedBonus, percentageBonus });

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
            deductionConceptRepository.Object,
            advanceRepository.Object,
            bonusRepository.Object,
            bonusConceptRepository.Object,
            liquidationRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new GeneratePayrollPeriodCommand((int)PayrollPeriodicity.Monthly, "2026-07", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.BonusLines.Should().HaveCount(2);
        persisted.NetAmount.Should().Be(345000m); // 300000 + 15000 (fijo) + 30000 (10% de 300000)
        fixedBonus.Status.Should().Be(PayrollBonusStatus.Applied);
        fixedBonus.PayrollLiquidationId.Should().Be(persisted.Id);
        percentageBonus.Status.Should().Be(PayrollBonusStatus.Applied);
    }
}
