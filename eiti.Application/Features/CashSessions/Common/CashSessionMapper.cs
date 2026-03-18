using eiti.Domain.Cash;
using eiti.Domain.Sales;

namespace eiti.Application.Features.CashSessions.Common;

internal static class CashSessionMapper
{
    private static readonly Dictionary<SalePaymentMethod, string> MethodNames = new()
    {
        { SalePaymentMethod.Cash,     "Efectivo" },
        { SalePaymentMethod.Transfer, "Transferencia" },
        { SalePaymentMethod.Card,     "Tarjeta" },
        { SalePaymentMethod.Check,    "Cheque" },
        { SalePaymentMethod.Other,    "Otros" }
    };

    public static CashSessionResponse Map(CashSession session, IReadOnlyList<SalePayment>? payments = null)
    {
        var breakdown = BuildBreakdown(payments ?? []);

        return new CashSessionResponse(
            session.Id.Value,
            session.BranchId.Value,
            session.CashDrawerId.Value,
            (int)session.Status,
            session.Status.ToString(),
            session.OpenedAt,
            session.ClosedAt,
            session.OpeningAmount,
            session.ExpectedClosingAmount,
            session.ActualClosingAmount,
            session.Difference,
            session.Notes,
            session.Movements
                .OrderByDescending(movement => movement.OccurredAt)
                .Select(movement => new CashSessionMovementResponse(
                    movement.Id.Value,
                    (int)movement.Type,
                    movement.Type.ToString(),
                    (int)movement.Direction,
                    movement.Direction.ToString(),
                    movement.Amount,
                    movement.OccurredAt,
                    movement.Description,
                    movement.ReferenceType,
                    movement.ReferenceId))
                .ToList(),
            breakdown);
    }

    public static CashSessionSummaryResponse MapSummary(CashSession session)
    {
        var salesIncome = session.Movements
            .Where(movement => movement.Type == CashMovementType.SaleIncome)
            .Sum(movement => movement.Amount);

        var withdrawals = session.Movements
            .Where(movement => movement.Type == CashMovementType.CashWithdrawal)
            .Sum(movement => movement.Amount);

        return new CashSessionSummaryResponse(
            session.Id.Value,
            session.OpeningAmount,
            salesIncome,
            withdrawals,
            session.ExpectedClosingAmount,
            session.ActualClosingAmount,
            session.Difference);
    }

    private static IReadOnlyList<PaymentMethodBreakdownItem> BuildBreakdown(IReadOnlyList<SalePayment> payments)
    {
        return payments
            .GroupBy(payment => payment.Method)
            .Select(group => new PaymentMethodBreakdownItem(
                (int)group.Key,
                MethodNames.GetValueOrDefault(group.Key, group.Key.ToString()),
                group.Sum(payment => payment.Amount)))
            .Where(item => item.Amount > 0)
            .OrderBy(item => item.Method)
            .ToList();
    }
}
