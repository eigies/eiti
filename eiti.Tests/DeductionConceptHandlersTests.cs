using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.SetDeductionConceptActive;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.UpdateDeductionConcept;
using eiti.Application.Features.Payroll.DeductionConcepts.Queries.ListDeductionConcepts;
using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class DeductionConceptHandlersTests
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
        var repository = new Mock<IPayrollDeductionConceptRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        PayrollDeductionConcept? persisted = null;
        repository
            .Setup(r => r.AddAsync(It.IsAny<PayrollDeductionConcept>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollDeductionConcept, CancellationToken>((c, _) => persisted = c)
            .Returns(Task.CompletedTask);

        var handler = new CreateDeductionConceptHandler(user.Object, repository.Object, unitOfWork.Object);

        var result = await handler.Handle(new CreateDeductionConceptCommand("Jubilacion", 11m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Jubilacion");
        result.Value.Percentage.Should().Be(11m);
    }

    [Fact]
    public async Task UpdateHandler_ShouldFail_WhenConceptNotFound()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var repository = new Mock<IPayrollDeductionConceptRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<PayrollDeductionConceptId>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollDeductionConcept?)null);

        var handler = new UpdateDeductionConceptHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new UpdateDeductionConceptCommand(Guid.NewGuid(), "Nuevo nombre", 5m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveHandler_ShouldDeactivate_WhenIsActiveFalse()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var concept = PayrollDeductionConcept.Create(companyId, "ART", 3m);
        var repository = new Mock<IPayrollDeductionConceptRepository>();
        repository
            .Setup(r => r.GetByIdAsync(concept.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(concept);

        var handler = new SetDeductionConceptActiveHandler(user.Object, repository.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new SetDeductionConceptActiveCommand(concept.Id.Value, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ListHandler_ShouldReturnMappedItems()
    {
        var companyId = CompanyId.New();
        var user = MockUser(companyId);
        var concept = PayrollDeductionConcept.Create(companyId, "Obra social", 3m);
        var repository = new Mock<IPayrollDeductionConceptRepository>();
        repository
            .Setup(r => r.ListByCompanyAsync(companyId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollDeductionConcept> { concept });

        var handler = new ListDeductionConceptsHandler(user.Object, repository.Object);

        var result = await handler.Handle(new ListDeductionConceptsQuery(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(x => x.Name == "Obra social");
    }

    [Fact]
    public async Task UpdateHandler_ShouldFail_WhenCompanyIdIsNull()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns((CompanyId?)null);

        var handler = new UpdateDeductionConceptHandler(user.Object, new Mock<IPayrollDeductionConceptRepository>().Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new UpdateDeductionConceptCommand(Guid.NewGuid(), "Nombre", 5m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveHandler_ShouldFail_WhenCompanyIdIsNull()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns((CompanyId?)null);

        var handler = new SetDeductionConceptActiveHandler(user.Object, new Mock<IPayrollDeductionConceptRepository>().Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new SetDeductionConceptActiveCommand(Guid.NewGuid(), true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ListHandler_ShouldFail_WhenCompanyIdIsNull()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns((CompanyId?)null);

        var handler = new ListDeductionConceptsHandler(user.Object, new Mock<IPayrollDeductionConceptRepository>().Object);

        var result = await handler.Handle(new ListDeductionConceptsQuery(true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
