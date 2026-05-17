using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Primitives;
using eiti.Domain.Sales;
using eiti.Domain.Users;

namespace eiti.Domain.Cash;

public sealed class CashSession : AggregateRoot<CashSessionId>
{
    public CompanyId CompanyId { get; private set; }
    public BranchId BranchId { get; private set; }
    public CashDrawerId CashDrawerId { get; private set; }
    public UserId OpenedByUserId { get; private set; }
    public UserId? ClosedByUserId { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public decimal OpeningAmount { get; private set; }
    public decimal? ActualClosingAmount { get; private set; }
    public CashSessionStatus Status { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<CashMovement> _movements = [];
    public IReadOnlyCollection<CashMovement> Movements => _movements;

    private CashSession()
    {
    }

    private CashSession(
        CashSessionId id,
        CompanyId companyId,
        BranchId branchId,
        CashDrawerId cashDrawerId,
        UserId openedByUserId,
        decimal openingAmount,
        string? notes)
        : base(id)
    {
        if (openingAmount < 0)
        {
            throw new ArgumentException("Opening amount cannot be negative.", nameof(openingAmount));
        }

        CompanyId = companyId;
        BranchId = branchId;
        CashDrawerId = cashDrawerId;
        OpenedByUserId = openedByUserId;
        OpenedAt = DateTime.UtcNow;
        OpeningAmount = openingAmount;
        Status = CashSessionStatus.Open;
        Notes = NormalizeOptional(notes, 255, "Notes");
    }

    public static CashSession Open(
        CompanyId companyId,
        BranchId branchId,
        CashDrawerId cashDrawerId,
        UserId openedByUserId,
        decimal openingAmount,
        string? notes)
    {
        var session = new CashSession(
            CashSessionId.New(),
            companyId,
            branchId,
            cashDrawerId,
            openedByUserId,
            openingAmount,
            notes);

        if (openingAmount > 0)
        {
            session.AddMovement(
                CashMovementType.OpeningFloat,
                CashMovementDirection.In,
                openingAmount,
                CashReferenceTypes.Session,
                session.Id.Value,
                "Opening float",
                openedByUserId);
        }

        return session;
    }

    public void RegisterSaleIncome(
        decimal amount,
        Guid saleId,
        UserId createdByUserId)
    {
        EnsureOpen();
        AddMovement(
            CashMovementType.SaleIncome,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.Sale,
            saleId,
            "Sale payment",
            createdByUserId);
    }

    public void RegisterTransferIncome(
        decimal amount,
        Guid saleId,
        UserId createdByUserId)
    {
        EnsureOpen();
        AddMovement(
            CashMovementType.TransferIncome,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.Sale,
            saleId,
            "Pago por transferencia",
            createdByUserId);
    }

    public void RegisterCardIncome(
        decimal amount,
        Guid saleId,
        UserId createdByUserId)
    {
        EnsureOpen();
        AddMovement(
            CashMovementType.CardIncome,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.Sale,
            saleId,
            "Pago con tarjeta",
            createdByUserId);
    }

    public void RegisterCcPaymentIncome(
        decimal amount,
        Guid saleId,
        UserId createdByUserId)
    {
        EnsureOpen();
        AddMovement(
            CashMovementType.CuentaCorrienteIncome,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.CuentaCorriente,
            saleId,
            "Pago cuenta corriente",
            createdByUserId);
    }

    public void RegisterCcPaymentCancel(
        decimal amount,
        Guid saleId,
        UserId createdByUserId)
    {
        EnsureOpen();
        AddMovement(
            CashMovementType.CuentaCorrienteCancellation,
            CashMovementDirection.Out,
            amount,
            CashReferenceTypes.CuentaCorriente,
            saleId,
            "Pago CC anulado",
            createdByUserId);
    }

    public void RegisterSaleCancellation(
        IEnumerable<SalePayment> payments,
        Guid saleId,
        UserId createdByUserId)
    {
        EnsureOpen();

        foreach (var payment in payments.Where(payment => payment.Amount > 0))
        {
            var (direction, description) = payment.Method switch
            {
                SalePaymentMethod.Cash => (CashMovementDirection.Out, "Venta cancelada - efectivo"),
                SalePaymentMethod.Transfer => (CashMovementDirection.None, "Venta cancelada - transferencia"),
                SalePaymentMethod.Card => (CashMovementDirection.None, "Venta cancelada - tarjeta"),
                SalePaymentMethod.Check => (CashMovementDirection.None, "Venta cancelada - cheque"),
                _ => (CashMovementDirection.None, "Venta cancelada - otros")
            };

            AddMovement(
                CashMovementType.SaleCancellation,
                direction,
                payment.Amount,
                CashReferenceTypes.Sale,
                saleId,
                description,
                createdByUserId);
        }
    }

    public void RegisterWithdrawal(
        decimal amount,
        string description,
        UserId createdByUserId)
    {
        EnsureOpen();

        if (ExpectedClosingAmount - amount < 0)
        {
            throw new InvalidOperationException("Cash withdrawal cannot leave a negative expected balance.");
        }

        AddMovement(
            CashMovementType.CashWithdrawal,
            CashMovementDirection.Out,
            amount,
            CashReferenceTypes.Withdrawal,
            null,
            description,
            createdByUserId);
    }

    public void RegisterTransferOut(
        decimal amount,
        Guid targetSessionId,
        string description,
        UserId createdByUserId)
    {
        EnsureOpen();

        if (ExpectedClosingAmount - amount < 0)
        {
            throw new InvalidOperationException("Cash transfer out cannot leave a negative expected balance.");
        }

        _movements.Add(CashMovement.Create(
            Id,
            CashMovementType.CashTransferOut,
            CashMovementDirection.Out,
            amount,
            CashReferenceTypes.Transfer,
            null,
            description,
            createdByUserId,
            targetSessionId));
    }

    public void RegisterTransferIn(
        decimal amount,
        Guid sourceSessionId,
        string description,
        UserId createdByUserId)
    {
        EnsureOpen();

        _movements.Add(CashMovement.Create(
            Id,
            CashMovementType.CashTransferIn,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.Transfer,
            null,
            description,
            createdByUserId,
            sourceSessionId));
    }

    public void Close(
        decimal actualClosingAmount,
        UserId closedByUserId,
        string? notes)
    {
        EnsureOpen();

        if (actualClosingAmount < 0)
        {
            throw new ArgumentException("Actual closing amount cannot be negative.", nameof(actualClosingAmount));
        }

        ActualClosingAmount = actualClosingAmount;
        ClosedByUserId = closedByUserId;
        ClosedAt = DateTime.UtcNow;
        Status = CashSessionStatus.Closed;
        Notes = NormalizeOptional(notes, 255, "Notes");
    }

    public decimal ExpectedClosingAmount =>
        _movements.Sum(movement =>
            movement.Type == CashMovementType.TransferIncome
                ? 0m  // transfers don't go into the physical drawer
                : movement.Direction == CashMovementDirection.In
                    ? movement.Amount
                    : movement.Direction == CashMovementDirection.Out
                        ? -movement.Amount
                        : 0m);

    public decimal Difference =>
        (ActualClosingAmount ?? ExpectedClosingAmount) - ExpectedClosingAmount;

    private void AddMovement(
        CashMovementType type,
        CashMovementDirection direction,
        decimal amount,
        string? referenceType,
        Guid? referenceId,
        string description,
        UserId createdByUserId)
    {
        _movements.Add(CashMovement.Create(
            Id,
            type,
            direction,
            amount,
            referenceType,
            referenceId,
            description,
            createdByUserId));
    }

    private void EnsureOpen()
    {
        if (Status != CashSessionStatus.Open)
        {
            throw new InvalidOperationException("The cash session is not open.");
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }
}
