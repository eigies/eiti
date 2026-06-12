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
        UserId createdByUserId,
        Guid? ccPaymentGroupId = null)
    {
        EnsureOpen();
        AddMovement(
            CashMovementType.CuentaCorrienteIncome,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.CuentaCorriente,
            saleId,
            "Pago cuenta corriente",
            createdByUserId,
            ccPaymentGroupId: ccPaymentGroupId);
    }

    public void RegisterCcTransferIncome(
        decimal amount,
        Guid saleId,
        UserId createdByUserId,
        Guid? ccPaymentGroupId = null)
    {
        EnsureOpen();
        AddMovement(
            CashMovementType.TransferIncome,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.CuentaCorriente,
            saleId,
            "Pago CC por transferencia",
            createdByUserId,
            ccPaymentGroupId: ccPaymentGroupId);
    }

    public void RegisterCcCardIncome(
        decimal amount,
        Guid saleId,
        UserId createdByUserId,
        Guid? ccPaymentGroupId = null)
    {
        EnsureOpen();
        AddMovement(
            CashMovementType.CardIncome,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.CuentaCorriente,
            saleId,
            "Pago CC con tarjeta",
            createdByUserId,
            ccPaymentGroupId: ccPaymentGroupId);
    }

    public void RegisterCcPaymentCancellation(
        IEnumerable<(SalePaymentMethod Method, decimal Amount)> lines,
        Guid saleId,
        UserId createdByUserId,
        Guid? ccPaymentGroupId = null)
    {
        EnsureOpen();

        // Reversa de un cobro de cuenta corriente, por método: el efectivo sale del cajón (Out)
        // y transferencia/tarjeta/cheque se registran como reversa neutra (None) para trazabilidad
        // sin tocar el efectivo esperado — espejo de RegisterSaleCancellation para ventas directas.
        foreach (var (method, amount) in lines.Where(line => line.Amount > 0))
        {
            var (direction, description) = method switch
            {
                SalePaymentMethod.Cash => (CashMovementDirection.Out, "Pago CC anulado - efectivo"),
                SalePaymentMethod.Transfer => (CashMovementDirection.None, "Pago CC anulado - transferencia"),
                SalePaymentMethod.Card => (CashMovementDirection.None, "Pago CC anulado - tarjeta"),
                SalePaymentMethod.Check => (CashMovementDirection.None, "Pago CC anulado - cheque"),
                _ => (CashMovementDirection.None, "Pago CC anulado - otros")
            };

            AddMovement(
                CashMovementType.CuentaCorrienteCancellation,
                direction,
                amount,
                CashReferenceTypes.CuentaCorriente,
                saleId,
                description,
                createdByUserId,
                ccPaymentGroupId: ccPaymentGroupId);
        }
    }

    public void RegisterSaleCancellation(
        IEnumerable<SalePayment> payments,
        Guid saleId,
        UserId createdByUserId,
        Guid? originalCashSessionId = null)
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
                createdByUserId,
                originalCashSessionId);
        }
    }

    public void RegisterPurchaseExpense(
        decimal amount,
        Guid purchaseId,
        UserId createdByUserId,
        Domain.Purchases.PurchasePaymentMethod method = Domain.Purchases.PurchasePaymentMethod.Cash)
    {
        EnsureOpen();
        var direction = method switch
        {
            Domain.Purchases.PurchasePaymentMethod.Cash => CashMovementDirection.Out,
            _                                           => CashMovementDirection.None,
        };
        var methodName = method switch
        {
            Domain.Purchases.PurchasePaymentMethod.Cash         => "Efectivo",
            Domain.Purchases.PurchasePaymentMethod.BankTransfer => "Transferencia",
            Domain.Purchases.PurchasePaymentMethod.Check        => "Cheque",
            _                                                    => "Otro",
        };
        AddMovement(
            CashMovementType.PurchaseExpense,
            direction,
            amount,
            CashReferenceTypes.Purchase,
            purchaseId,
            methodName,
            createdByUserId);
    }

    public void RegisterPurchasePaymentCancel(
        decimal amount,
        Guid purchaseId,
        UserId createdByUserId)
    {
        EnsureOpen();
        AddMovement(
            CashMovementType.PurchasePaymentCancellation,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.Purchase,
            purchaseId,
            "Pago de compra anulado",
            createdByUserId);
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

    public void RegisterDeposit(
        decimal amount,
        string description,
        UserId createdByUserId)
    {
        EnsureOpen();

        AddMovement(
            CashMovementType.CashDeposit,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.Deposit,
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
            transferCounterpartSessionId: targetSessionId));
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
            transferCounterpartSessionId: sourceSessionId));
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
            movement.Type is CashMovementType.TransferIncome or CashMovementType.CardIncome
                ? 0m  // non-cash payments don't go into the physical drawer
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
        UserId createdByUserId,
        Guid? originalCashSessionId = null,
        Guid? ccPaymentGroupId = null)
    {
        _movements.Add(CashMovement.Create(
            Id,
            type,
            direction,
            amount,
            referenceType,
            referenceId,
            description,
            createdByUserId,
            ccPaymentGroupId: ccPaymentGroupId,
            originalCashSessionId: originalCashSessionId));
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
