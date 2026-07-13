using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.SetBonusConceptActive;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.UpdateBonusConcept;
using eiti.Application.Features.Payroll.BonusConcepts.Queries.ListBonusConcepts;
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class BonusConceptHandlersTests
{
    private static Mock<ICurrentUserService> MockUser(CompanyId companyId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(companyId);
        return user;
    }

    [Fact]
    public async Task CreateHandler_ShouldPersistConcept_AndReturnResponse()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var repository = new Mock<IPayrollBonusConceptRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        PayrollBonusConcept? persisted = null;
        repository
            .Setup(r => r.AddAsync(It.IsAny<PayrollBonusConcept>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollBonusConcept, CancellationToken>((c, _) => persisted = c)
            .Returns(Task.CompletedTask);

        var handler = new CreateBonusConceptHandler(user.Object, repository.Object, unitOfWork.Object);

        var result = await handler.Handle(new CreateBonusConceptCommand("Presentismo"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Presentismo");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateHandler_ShouldFail_WhenConceptNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var repository = new Mock<IPayrollBonusConceptRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollBonusConceptId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollBonusConcept?)null);

        var handler = new UpdateBonusConceptHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new UpdateBonusConceptCommand(Guid.NewGuid(), "Nuevo nombre"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveHandler_ShouldDeactivate_WhenIsActiveFalse()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var concept = PayrollBonusConcept.Create(companyId, "Bonificacion por venta");
        var repository = new Mock<IPayrollBonusConceptRepository>();
        repository
            .Setup(r => r.GetByIdAsync(concept.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(concept);

        var handler = new SetBonusConceptActiveHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new SetBonusConceptActiveCommand(concept.Id.Value, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        concept.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ListHandler_ShouldReturnConcepts_FilteredByActiveOnly()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var concepts = new List<PayrollBonusConcept> { PayrollBonusConcept.Create(companyId, "Presentismo") };
        var repository = new Mock<IPayrollBonusConceptRepository>();
        repository
            .Setup(r => r.ListByCompanyAsync(companyId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(concepts);

        var handler = new ListBonusConceptsHandler(user.Object, repository.Object);

        var result = await handler.Handle(new ListBonusConceptsQuery(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Presentismo");
    }
}
