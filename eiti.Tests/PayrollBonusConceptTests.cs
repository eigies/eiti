using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using FluentAssertions;

namespace eiti.Tests;

public sealed class PayrollBonusConceptTests
{
    [Fact]
    public void Create_ShouldStartActive()
    {
        var concept = PayrollBonusConcept.Create(CompanyId.New(), "Presentismo");

        concept.Name.Should().Be("Presentismo");
        concept.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNameIsEmpty()
    {
        var act = () => PayrollBonusConcept.Create(CompanyId.New(), "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNameExceeds150Characters()
    {
        var act = () => PayrollBonusConcept.Create(CompanyId.New(), new string('a', 151));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_ShouldChangeName()
    {
        var concept = PayrollBonusConcept.Create(CompanyId.New(), "Presentismo");

        concept.Update("Bonificacion por venta");

        concept.Name.Should().Be("Bonificacion por venta");
    }

    [Fact]
    public void Deactivate_ThenActivate_ShouldToggleIsActive()
    {
        var concept = PayrollBonusConcept.Create(CompanyId.New(), "Presentismo");

        concept.Deactivate();
        concept.IsActive.Should().BeFalse();

        concept.Activate();
        concept.IsActive.Should().BeTrue();
    }
}
