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
        var breakdown = BuildBreakdown(session.Movements, payments ?? []);

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
                    movement.OriginalCashSessionId,
                    movement.PaymentMethod,
                    movement.SaleCcPaymentId,
                    movement.SupplierPaymentId))
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
                movement.Type == CashMovementType.CuentaCorrienteIncome ||
                movement.Type == CashMovementType.SaleCancellation ||
                movement.Type == CashMovementType.CuentaCorrienteCancellation)
            .Sum(movement =>
                movement.Type is CashMovementType.SaleCancellation or CashMovementType.CuentaCorrienteCancellation
                    ? -movement.Amount
                    : movement.Amount);

        var withdrawals = session.Movements
            .Where(movement => movement.Type == CashMovementType.CashWithdrawal)
            .Sum(movement => movement.Amount);

        var manualDeposits = session.Movements
            .Where(movement => movement.Type == CashMovementType.CashDeposit)
            .Sum(movement => movement.Amount);

        var salesCancellations = session.Movements
            .Where(movement => movement.Type is CashMovementType.SaleCancellation or CashMovementType.CuentaCorrienteCancellation)
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
        IEnumerable<CashMovement> movements,
        IReadOnlyList<SalePayment> payments)
    {
        var saleEntries = payments
            .Where(p => p.Method != SalePaymentMethod.CustomerCredit)
            .Select(p => (p.Method, Amount: p.Amount, Surcharge: p.Method == SalePaymentMethod.Card ? (p.CardSurchargeAmt ?? 0m) : 0m));

        var ccEntries = movements
            .Select(ToCuentaCorrienteBreakdownEntry)
            .Where(entry => entry is not null)
            .Select(entry => entry!.Value);

        return saleEntries
            .Concat(ccEntries)
            .GroupBy(e => e.Method)
            .Select(g => new PaymentMethodBreakdownItem(
                (int)g.Key,
                MethodNames.GetValueOrDefault(g.Key, g.Key.ToString()),
                g.Sum(e => e.Amount - e.Surcharge),
                g.Sum(e => e.Surcharge)))
            .Where(item => item.Amount != 0 || item.SurchargeAmount != 0)
            .OrderBy(item => item.Method)
            .ToList();
    }

    private static (SalePaymentMethod Method, decimal Amount, decimal Surcharge)? ToCuentaCorrienteBreakdownEntry(CashMovement movement)
    {
        if (movement.ReferenceType != CashReferenceTypes.CuentaCorriente
            && movement.Type != CashMovementType.CuentaCorrienteCancellation)
        {
            return null;
        }

        var method = ResolveSalePaymentMethod(movement);
        if (method is null || method == SalePaymentMethod.CustomerCredit)
        {
            return null;
        }

        var sign = movement.Type == CashMovementType.CuentaCorrienteCancellation ? -1m : 1m;
        return (method.Value, sign * movement.Amount, 0m);
    }

    private static SalePaymentMethod? ResolveSalePaymentMethod(CashMovement movement)
    {
        if (movement.PaymentMethod.HasValue
            && Enum.IsDefined(typeof(SalePaymentMethod), movement.PaymentMethod.Value))
        {
            return (SalePaymentMethod)movement.PaymentMethod.Value;
        }

        var description = movement.Description.ToLowerInvariant();
        if (description.Contains("transferencia")) return SalePaymentMethod.Transfer;
        if (description.Contains("tarjeta")) return SalePaymentMethod.Card;
        if (description.Contains("cheque")) return SalePaymentMethod.Check;
        if (description.Contains("otros")) return SalePaymentMethod.Other;
        if (description.Contains("efectivo")) return SalePaymentMethod.Cash;

        return movement.Type switch
        {
            CashMovementType.TransferIncome => SalePaymentMethod.Transfer,
            CashMovementType.CardIncome => SalePaymentMethod.Card,
            CashMovementType.CuentaCorrienteIncome => SalePaymentMethod.Cash,
            _ => null
        };
    }
}
