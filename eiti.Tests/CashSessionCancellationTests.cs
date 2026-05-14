using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Sales;
using eiti.Domain.Users;
using FluentAssertions;

namespace eiti.Tests;

public sealed class CashSessionCancellationTests
{
    [Fact]
    public void RegisterSaleCancellation_ShouldDiscountCashFromExpectedBalance_AndKeepOtherMethodsOutOfPhysicalCash()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var drawerId = CashDrawerId.New();
        var userId = UserId.New();
        var saleId = Guid.NewGuid();

        var session = CashSession.Open(companyId, branchId, drawerId, userId, 200m, null);
        session.RegisterSaleIncome(100m, saleId, userId);
        session.RegisterTransferIncome(50m, saleId, userId);

        session.RegisterSaleCancellation(
            [
                SalePayment.Create(SalePaymentMethod.Cash, 100m, null),
                SalePayment.Create(SalePaymentMethod.Transfer, 50m, "TRX-001"),
                SalePayment.Create(SalePaymentMethod.Card, 25m, "CARD-001")
            ],
            saleId,
            userId);

        session.ExpectedClosingAmount.Should().Be(200m);

        var cancellationMovements = session.Movements
            .Where(m => m.Type == CashMovementType.SaleCancellation)
            .ToList();

        cancellationMovements.Should().HaveCount(3);
        cancellationMovements.Should().ContainSingle(m =>
            m.Amount == 100m &&
            m.Direction == CashMovementDirection.Out &&
            m.Description == "Venta cancelada - efectivo");
        cancellationMovements.Should().ContainSingle(m =>
            m.Amount == 50m &&
            m.Direction == CashMovementDirection.None &&
            m.Description == "Venta cancelada - transferencia");
        cancellationMovements.Should().ContainSingle(m =>
            m.Amount == 25m &&
            m.Direction == CashMovementDirection.None &&
            m.Description == "Venta cancelada - tarjeta");
    }
}
