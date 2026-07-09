using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using FluentAssertions;

namespace eiti.Tests;

public sealed class PayrollLiquidationTests
{
    private static PayrollLiquidation CreateLiquidation(
        decimal grossAmount = 500000m,
        IReadOnlyList<PayrollLiquidationDeductionLine>? deductions = null,
        IReadOnlyList<PayrollLiquidationAdvanceLine>? advances = null)
    {
        return PayrollLiquidation.Create(
            CompanyId.New(),
            EmployeeId.New(),
            null,
            "2026-07",
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 31),
            grossAmount,
            deductions ?? [],
            advances ?? []);
    }

    [Fact]
    public void Create_ShouldStartAsPending_WithNetEqualsGross_WhenNoLinesGiven()
    {
        var liquidation = CreateLiquidation(500000m);

        liquidation.Status.Should().Be(PayrollLiquidationStatus.Pending);
        liquidation.NetAmount.Should().Be(500000m);
    }

    [Fact]
    public void Create_ShouldComputeNetAmount_SubtractingDeductionsAndAdvances()
    {
        var deductions = new List<PayrollLiquidationDeductionLine>
        {
            PayrollLiquidationDeductionLine.Create("Jubilacion", 11m, 55000m),
            PayrollLiquidationDeductionLine.Create("Obra social", 3m, 15000m)
        };
        var advances = new List<PayrollLiquidationAdvanceLine>
        {
            PayrollLiquidationAdvanceLine.Create(Guid.NewGuid(), 20000m)
        };

        var liquidation = CreateLiquidation(500000m, deductions, advances);

        liquidation.NetAmount.Should().Be(410000m);
    }

    [Fact]
    public void Create_ShouldThrow_WhenGrossAmountIsZeroOrLess()
    {
        var act = () => CreateLiquidation(0m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsPaid_ShouldRequireCashSessionId_WhenMethodIsCash()
    {
        var liquidation = CreateLiquidation();

        var act = () => liquidation.MarkAsPaid(PayrollPaymentMethod.Cash, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsPaid_ShouldSetPaidAndClearCashSessionId_WhenMethodIsTransfer()
    {
        var liquidation = CreateLiquidation();

        liquidation.MarkAsPaid(PayrollPaymentMethod.Transfer, null);

        liquidation.Status.Should().Be(PayrollLiquidationStatus.Paid);
        liquidation.CashSessionId.Should().BeNull();
        liquidation.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsPaid_ShouldSetCashSessionId_WhenMethodIsCash()
    {
        var liquidation = CreateLiquidation();
        var cashSessionId = Guid.NewGuid();

        liquidation.MarkAsPaid(PayrollPaymentMethod.Cash, cashSessionId);

        liquidation.CashSessionId.Should().Be(cashSessionId);
    }

    [Fact]
    public void MarkAsPaid_ShouldThrow_WhenAlreadyPaid()
    {
        var liquidation = CreateLiquidation();
        liquidation.MarkAsPaid(PayrollPaymentMethod.Transfer, null);

        var act = () => liquidation.MarkAsPaid(PayrollPaymentMethod.Transfer, null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_ShouldSetCancelled()
    {
        var liquidation = CreateLiquidation();

        liquidation.Cancel();

        liquidation.Status.Should().Be(PayrollLiquidationStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldThrow_WhenAlreadyCancelled()
    {
        var liquidation = CreateLiquidation();
        liquidation.Cancel();

        var act = () => liquidation.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }
}
