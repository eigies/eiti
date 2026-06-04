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

    public static CashSessionResponse Map(
        CashSession session,
        IReadOnlyList<SalePayment>? payments = null,
        Dictionary<Guid, string?>? saleCodes = null,
        Dictionary<Guid, string>? usernames = null,
        IReadOnlyList<SaleCcPayment>? ccPayments = null)
    {
        var breakdown = BuildBreakdown(payments ?? [], ccPayments);

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
                    movement.ReferenceId,
                    movement.ReferenceId.HasValue && saleCodes != null
                        ? saleCodes.GetValueOrDefault(movement.ReferenceId.Value)
                        : null,
                    usernames?.GetValueOrDefault(movement.CreatedByUserId.Value),
                    movement.OriginalCashSessionId))
                .ToList(),
            breakdown);
    }

    public static CashSessionSummaryResponse MapSummary(
        CashSession session,
        IReadOnlyList<SalePayment>? payments = null,
        IReadOnlyDictionary<int, string>? bankNames = null,
        IReadOnlyList<SaleCcPayment>? ccPayments = null)
    {
        var salesIncome = session.Movements
            .Where(movement =>
                movement.Type == CashMovementType.SaleIncome ||
                movement.Type == CashMovementType.CardIncome ||
                movement.Type == CashMovementType.TransferIncome ||
                movement.Type == CashMovementType.CuentaCorrienteIncome)
            .Sum(movement => movement.Amount);

        var withdrawals = session.Movements
            .Where(movement => movement.Type == CashMovementType.CashWithdrawal)
            .Sum(movement => movement.Amount);

        var manualDeposits = session.Movements
            .Where(movement => movement.Type == CashMovementType.CashDeposit)
            .Sum(movement => movement.Amount);

        var salesCancellations = session.Movements
            .Where(movement => movement.Type == CashMovementType.SaleCancellation)
            .Sum(movement => movement.Amount);

        var transferBreakdown = BuildTransferBankBreakdown(payments ?? [], bankNames ?? new Dictionary<int, string>());

        return new CashSessionSummaryResponse(
            session.Id.Value,
            session.OpeningAmount,
            salesIncome,
            manualDeposits,
            withdrawals,
            salesCancellations,
            session.ExpectedClosingAmount,
            session.ActualClosingAmount,
            session.Difference,
            transferBreakdown);
    }

    private static IReadOnlyList<TransferBankBreakdownItem> BuildTransferBankBreakdown(
        IReadOnlyList<SalePayment> payments,
        IReadOnlyDictionary<int, string> bankNames)
    {
        return payments
            .Where(p => p.Method == SalePaymentMethod.Transfer && p.TransferBankId.HasValue)
            .GroupBy(p => p.TransferBankId!.Value)
            .Select(g => new TransferBankBreakdownItem(
                g.Key,
                bankNames.GetValueOrDefault(g.Key, "Banco desconocido"),
                g.Sum(p => p.Amount)))
            .OrderBy(x => x.BankName)
            .ToList();
    }

    private static IReadOnlyList<PaymentMethodBreakdownItem> BuildBreakdown(
        IReadOnlyList<SalePayment> payments,
        IReadOnlyList<SaleCcPayment>? ccPayments = null)
    {
        var allEntries = payments
            .Where(p => p.Method != SalePaymentMethod.CustomerCredit)
            .Select(p => (p.Method, p.Amount, Surcharge: p.Method == SalePaymentMethod.Card ? (p.CardSurchargeAmt ?? 0m) : 0m))
            .Concat((ccPayments ?? [])
                .Where(p => p.Method != SalePaymentMethod.CustomerCredit)
                .Select(p => (p.Method, p.Amount, Surcharge: p.Method == SalePaymentMethod.Card ? (p.CardSurchargeAmt ?? 0m) : 0m)));

        return allEntries
            .GroupBy(e => e.Method)
            .Select(g => new PaymentMethodBreakdownItem(
                (int)g.Key,
                MethodNames.GetValueOrDefault(g.Key, g.Key.ToString()),
                g.Sum(e => e.Amount - e.Surcharge),
                g.Sum(e => e.Surcharge)))
            .Where(item => item.Amount > 0)
            .OrderBy(item => item.Method)
            .ToList();
    }
}
