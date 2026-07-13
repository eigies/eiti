using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.Bonuses.Commands.CancelPayrollBonus;
using eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;
using eiti.Application.Features.Payroll.Bonuses.Queries.ListPayrollBonuses;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class PayrollBonusHandlersTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        return user;
    }

    [Fact]
    public async Task CreateHandler_ShouldFail_WhenEmployeeNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<EmployeeId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);
        var conceptRepository = new Mock<IPayrollBonusConceptRepository>();
        var bonusRepository = new Mock<IPayrollBonusRepository>();

        var handler = new CreatePayrollBonusHandler(user.Object, bonusRepository.Object, conceptRepository.Object, employeeRepository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreatePayrollBonusCommand(Guid.NewGuid(), Guid.NewGuid(), (int)PayrollBonusAmountType.FixedAmount, 15000m, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateHandler_ShouldFail_WhenConceptNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var employee = Employee.Create(companyId, null, "Juan", "Perez", null, null, null, EmployeeRole.Staff);
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<EmployeeId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        var conceptRepository = new Mock<IPayrollBonusConceptRepository>();
        conceptRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollBonusConceptId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollBonusConcept?)null);
        var bonusRepository = new Mock<IPayrollBonusRepository>();

        var handler = new CreatePayrollBonusHandler(user.Object, bonusRepository.Object, conceptRepository.Object, employeeRepository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreatePayrollBonusCommand(employee.Id.Value, Guid.NewGuid(), (int)PayrollBonusAmountType.FixedAmount, 15000m, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateHandler_ShouldPersistBonus_AndReturnResponse()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var employee = Employee.Create(companyId, null, "Juan", "Perez", null, null, null, EmployeeRole.Staff);
        var concept = PayrollBonusConcept.Create(companyId, "Presentismo");
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<EmployeeId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        var conceptRepository = new Mock<IPayrollBonusConceptRepository>();
        conceptRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollBonusConceptId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(concept);
        var bonusRepository = new Mock<IPayrollBonusRepository>();
        PayrollBonus? persisted = null;
        bonusRepository
            .Setup(r => r.AddAsync(It.IsAny<PayrollBonus>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollBonus, CancellationToken>((b, _) => persisted = b)
            .Returns(Task.CompletedTask);

        var handler = new CreatePayrollBonusHandler(user.Object, bonusRepository.Object, conceptRepository.Object, employeeRepository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreatePayrollBonusCommand(employee.Id.Value, concept.Id.Value, (int)PayrollBonusAmountType.Percentage, 10m, "Julio"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(PayrollBonusStatus.Pending);
        result.Value.AmountType.Should().Be((int)PayrollBonusAmountType.Percentage);
    }

    [Fact]
    public async Task CancelHandler_ShouldCancelPendingBonus()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var bonus = PayrollBonus.Create(companyId, EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.FixedAmount, 15000m, null);
        var bonusRepository = new Mock<IPayrollBonusRepository>();
        bonusRepository
            .Setup(r => r.GetByIdAsync(bonus.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bonus);

        var handler = new CancelPayrollBonusHandler(user.Object, bonusRepository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelPayrollBonusCommand(bonus.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        bonus.Status.Should().Be(PayrollBonusStatus.Cancelled);
    }

    [Fact]
    public async Task CancelHandler_ShouldFail_WhenNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var bonusRepository = new Mock<IPayrollBonusRepository>();
        bonusRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollBonusId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollBonus?)null);

        var handler = new CancelPayrollBonusHandler(user.Object, bonusRepository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelPayrollBonusCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ListHandler_ShouldReturnBonuses()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var bonuses = new List<PayrollBonus> { PayrollBonus.Create(companyId, EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.FixedAmount, 15000m, null) };
        var bonusRepository = new Mock<IPayrollBonusRepository>();
        bonusRepository
            .Setup(r => r.ListByCompanyAsync(companyId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bonuses);

        var handler = new ListPayrollBonusesHandler(user.Object, bonusRepository.Object);

        var result = await handler.Handle(new ListPayrollBonusesQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }
}
