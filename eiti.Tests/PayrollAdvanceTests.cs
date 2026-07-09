using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using eiti.Domain.Users;
using FluentAssertions;

namespace eiti.Tests;

public sealed class PayrollAdvanceTests
{
    private static PayrollAdvance CreateAdvance(decimal amount = 10000m)
    {
        return PayrollAdvance.Create(
            CompanyId.New(),
            EmployeeId.New(),
            amount,
            DateTime.UtcNow,
            "Adelanto por reparacion urgente",
            UserId.New());
    }

    [Fact]
    public void Create_ShouldStartAsPending()
    {
        var advance = CreateAdvance();

        advance.Status.Should().Be(PayrollAdvanceStatus.Pending);
        advance.AppliedToLiquidationId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldThrow_WhenAmountIsZeroOrLess()
    {
        var act = () => CreateAdvance(0m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_ShouldSetCancelled_WhenPending()
    {
        var advance = CreateAdvance();

        advance.Cancel();

        advance.Status.Should().Be(PayrollAdvanceStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldThrow_WhenNotPending()
    {
        var advance = CreateAdvance();
        advance.Cancel();

        var act = () => advance.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Apply_ShouldSetAppliedAndLiquidationId()
    {
        var advance = CreateAdvance();
        var liquidationId = PayrollLiquidationId.New();

        advance.Apply(liquidationId);

        advance.Status.Should().Be(PayrollAdvanceStatus.Applied);
        advance.AppliedToLiquidationId.Should().Be(liquidationId);
    }

    [Fact]
    public void Revert_ShouldSetPendingAndClearLiquidationId()
    {
        var advance = CreateAdvance();
        advance.Apply(PayrollLiquidationId.New());

        advance.Revert();

        advance.Status.Should().Be(PayrollAdvanceStatus.Pending);
        advance.AppliedToLiquidationId.Should().BeNull();
    }

    [Fact]
    public void Revert_ShouldThrow_WhenNotApplied()
    {
        var advance = CreateAdvance();

        var act = () => advance.Revert();

        act.Should().Throw<InvalidOperationException>();
    }
}
