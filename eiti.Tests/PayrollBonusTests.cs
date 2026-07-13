using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;

namespace eiti.Tests;

public sealed class PayrollBonusTests
{
    private static PayrollBonus CreateFixedBonus(decimal value = 15000m) =>
        PayrollBonus.Create(CompanyId.New(), EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.FixedAmount, value, "Presentismo de julio");

    private static PayrollBonus CreatePercentageBonus(decimal value = 10m) =>
        PayrollBonus.Create(CompanyId.New(), EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.Percentage, value, null);

    [Fact]
    public void Create_ShouldStartAsPending()
    {
        var bonus = CreateFixedBonus();

        bonus.Status.Should().Be(PayrollBonusStatus.Pending);
        bonus.PayrollLiquidationId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldThrow_WhenValueIsZeroOrLess()
    {
        var act = () => PayrollBonus.Create(CompanyId.New(), EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.FixedAmount, 0m, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenPercentageExceeds100()
    {
        var act = () => PayrollBonus.Create(CompanyId.New(), EmployeeId.New(), PayrollBonusConceptId.New(), PayrollBonusAmountType.Percentage, 101m, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldAllowFixedAmountAboveOneHundred()
    {
        var bonus = CreateFixedBonus(500000m);

        bonus.Value.Should().Be(500000m);
    }

    [Fact]
    public void Resolve_ShouldReturnValue_WhenFixedAmount()
    {
        var bonus = CreateFixedBonus(15000m);

        bonus.Resolve(300000m).Should().Be(15000m);
    }

    [Fact]
    public void Resolve_ShouldReturnPercentageOfBaseSalary_WhenPercentage()
    {
        var bonus = CreatePercentageBonus(10m);

        bonus.Resolve(300000m).Should().Be(30000m);
    }

    [Fact]
    public void Apply_ShouldSetAppliedAndLiquidationId()
    {
        var bonus = CreateFixedBonus();
        var liquidationId = PayrollLiquidationId.New();

        bonus.Apply(liquidationId);

        bonus.Status.Should().Be(PayrollBonusStatus.Applied);
        bonus.PayrollLiquidationId.Should().Be(liquidationId);
    }

    [Fact]
    public void Apply_ShouldThrow_WhenNotPending()
    {
        var bonus = CreateFixedBonus();
        bonus.Apply(PayrollLiquidationId.New());

        var act = () => bonus.Apply(PayrollLiquidationId.New());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_ShouldSetCancelled_WhenPending()
    {
        var bonus = CreateFixedBonus();

        bonus.Cancel();

        bonus.Status.Should().Be(PayrollBonusStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldThrow_WhenNotPending()
    {
        var bonus = CreateFixedBonus();
        bonus.Apply(PayrollLiquidationId.New());

        var act = () => bonus.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RevertToPending_ShouldClearLiquidationId_WhenApplied()
    {
        var bonus = CreateFixedBonus();
        bonus.Apply(PayrollLiquidationId.New());

        bonus.RevertToPending();

        bonus.Status.Should().Be(PayrollBonusStatus.Pending);
        bonus.PayrollLiquidationId.Should().BeNull();
    }

    [Fact]
    public void RevertToPending_ShouldThrow_WhenNotApplied()
    {
        var bonus = CreateFixedBonus();

        var act = () => bonus.RevertToPending();

        act.Should().Throw<InvalidOperationException>();
    }
}
