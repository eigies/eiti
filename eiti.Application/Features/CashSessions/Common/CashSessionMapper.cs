using eiti.Domain.Cash;

namespace eiti.Application.Features.CashSessions.Common;

internal static class CashSessionMapper
{
    public static CashSessionResponse Map(CashSession session)
    {
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
                .ToList());
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
}
