using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using FluentAssertions;

namespace eiti.Tests;

public sealed class CashSessionPayrollTests
{
    private static CashSession CreateOpenSession(out UserId userId)
    {
        userId = UserId.New();
        var session = CashSession.Open(
            CompanyId.New(),
            BranchId.New(),
            CashDrawerId.New(),
            userId,
            openingAmount: 100000m,
            notes: null);
        return session;
    }

    [Fact]
    public void RegisterPayrollExpense_ShouldAddOutMovement_TaggedWithLiquidationId()
    {
        var session = CreateOpenSession(out var userId);
        var liquidationId = Guid.NewGuid();

        session.RegisterPayrollExpense(50000m, liquidationId, userId);

        var movement = session.Movements.Single(m => m.Type == CashMovementType.PayrollExpense);
        movement.Direction.Should().Be(CashMovementDirection.Out);
        movement.Amount.Should().Be(50000m);
        movement.PayrollLiquidationId.Should().Be(liquidationId);
    }

    [Fact]
    public void RegisterPayrollExpenseCancel_ShouldAddInMovement_ReversingTheExpense()
    {
        var session = CreateOpenSession(out var userId);
        var liquidationId = Guid.NewGuid();
        session.RegisterPayrollExpense(50000m, liquidationId, userId);

        session.RegisterPayrollExpenseCancel(50000m, liquidationId, userId);

        var cancelMovement = session.Movements.Single(m => m.Type == CashMovementType.PayrollExpenseCancellation);
        cancelMovement.Direction.Should().Be(CashMovementDirection.In);
        cancelMovement.PayrollLiquidationId.Should().Be(liquidationId);
    }

    [Fact]
    public void RegisterPayrollAdvanceExpense_ShouldAddOutMovement_TaggedWithAdvanceId()
    {
        var session = CreateOpenSession(out var userId);
        var advanceId = Guid.NewGuid();

        session.RegisterPayrollAdvanceExpense(15000m, advanceId, userId);

        var movement = session.Movements.Single(m => m.Type == CashMovementType.PayrollAdvanceExpense);
        movement.Direction.Should().Be(CashMovementDirection.Out);
        movement.PayrollAdvanceId.Should().Be(advanceId);
    }
}
