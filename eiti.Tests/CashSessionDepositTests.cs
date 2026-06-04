using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using FluentAssertions;

namespace eiti.Tests;

public sealed class CashSessionDepositTests
{
    [Fact]
    public void RegisterDeposit_ShouldAddCashToExpectedBalance()
    {
        var userId = UserId.New();
        var session = CashSession.Open(
            CompanyId.New(),
            BranchId.New(),
            CashDrawerId.New(),
            userId,
            200m,
            null);

        session.RegisterDeposit(150m, "Retiro del banco", userId);

        session.ExpectedClosingAmount.Should().Be(350m);
        session.Movements.Should().ContainSingle(movement =>
            movement.Type == CashMovementType.CashDeposit
            && movement.Direction == CashMovementDirection.In
            && movement.Amount == 150m
            && movement.ReferenceType == CashReferenceTypes.Deposit
            && movement.Description == "Retiro del banco");
    }
}
