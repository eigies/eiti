using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using FluentAssertions;

namespace eiti.Tests;

public sealed class PayrollDeductionConceptTests
{
    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var concept = PayrollDeductionConcept.Create(CompanyId.New(), "Jubilacion", 11m);

        concept.Name.Should().Be("Jubilacion");
        concept.Percentage.Should().Be(11m);
        concept.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_ShouldThrow_WhenPercentageOutOfRange(decimal percentage)
    {
        var act = () => PayrollDeductionConcept.Create(CompanyId.New(), "Obra social", percentage);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNameEmpty()
    {
        var act = () => PayrollDeductionConcept.Create(CompanyId.New(), "  ", 5m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_ShouldChangeNameAndPercentage()
    {
        var concept = PayrollDeductionConcept.Create(CompanyId.New(), "ART", 3m);

        concept.Update("ART actualizado", 4.5m);

        concept.Name.Should().Be("ART actualizado");
        concept.Percentage.Should().Be(4.5m);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var concept = PayrollDeductionConcept.Create(CompanyId.New(), "ART", 3m);

        concept.Deactivate();

        concept.IsActive.Should().BeFalse();
    }
}
