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
            createdByUserId,
            paymentMethod: (int)SalePaymentMethod.Cash);
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
            createdByUserId,
            paymentMethod: (int)SalePaymentMethod.Transfer);
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
            createdByUserId,
            paymentMethod: (int)SalePaymentMethod.Card);
    }

    public void RegisterCcPaymentIncome(
        decimal amount,
        Guid saleId,
        UserId createdByUserId,
        Guid? ccPaymentGroupId = null,
        Guid? saleCcPaymentId = null)
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
            ccPaymentGroupId: ccPaymentGroupId,
            paymentMethod: (int)SalePaymentMethod.Cash,
            saleCcPaymentId: saleCcPaymentId);
    }

    public void RegisterCcTransferIncome(
        decimal amount,
        Guid saleId,
        UserId createdByUserId,
        Guid? ccPaymentGroupId = null,
        Guid? saleCcPaymentId = null)
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
            ccPaymentGroupId: ccPaymentGroupId,
            paymentMethod: (int)SalePaymentMethod.Transfer,
            saleCcPaymentId: saleCcPaymentId);
    }

    public void RegisterCcCardIncome(
        decimal amount,
        Guid saleId,
        UserId createdByUserId,
        Guid? ccPaymentGroupId = null,
        Guid? saleCcPaymentId = null)
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
            ccPaymentGroupId: ccPaymentGroupId,
            paymentMethod: (int)SalePaymentMethod.Card,
            saleCcPaymentId: saleCcPaymentId);
    }

    public void RegisterCcNonCashIncome(
        SalePaymentMethod method,
        decimal amount,
        Guid saleId,
        UserId createdByUserId,
        Guid? ccPaymentGroupId = null,
        Guid? saleCcPaymentId = null)
    {
        EnsureOpen();

        var description = method switch
        {
            SalePaymentMethod.Check => "Pago CC con cheque",
            SalePaymentMethod.Other => "Pago CC otros",
            _ => "Pago CC no efectivo"
        };

        AddMovement(
            CashMovementType.CuentaCorrienteIncome,
            CashMovementDirection.None,
            amount,
            CashReferenceTypes.CuentaCorriente,
            saleId,
            description,
            createdByUserId,
            ccPaymentGroupId: ccPaymentGroupId,
            paymentMethod: (int)method,
            saleCcPaymentId: saleCcPaymentId);
    }

    public void RegisterCcPaymentCancellation(
        IEnumerable<(SalePaymentMethod Method, decimal Amount, Guid? SaleCcPaymentId)> lines,
        Guid saleId,
        UserId createdByUserId,
        Guid? ccPaymentGroupId = null)
    {
        EnsureOpen();

        // Reversa de un cobro de cuenta corriente, por método: el efectivo sale del cajón (Out)
        // y transferencia/tarjeta/cheque se registran como reversa neutra (None) para trazabilidad
        // sin tocar el efectivo esperado — espejo de RegisterSaleCancellation para ventas directas.
        foreach (var (method, amount, saleCcPaymentId) in lines.Where(line => line.Amount > 0))
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
                ccPaymentGroupId: ccPaymentGroupId,
                paymentMethod: (int)method,
                saleCcPaymentId: saleCcPaymentId);
        }
    }

    // Cobro a nivel cliente (bolsa). UN asiento por cobro según método, espejo de los CC income:
    // efectivo entra al cajón (In, suma al esperado); transferencia/tarjeta In pero excluidos del esperado
    // (ExpectedClosingAmount zeroes TransferIncome/CardIncome); cheque/otros visibles con dirección None.
    // ReferenceType = CustomerPayment para quedar fuera del índice anti-duplicado (filtrado a 'Sale').
    public void RegisterCustomerPaymentIncome(
        SalePaymentMethod method,
        decimal amount,
        Guid customerPaymentId,
        UserId createdByUserId)
    {
        EnsureOpen();

        var (type, direction, description) = method switch
        {
            SalePaymentMethod.Cash     => (CashMovementType.CuentaCorrienteIncome, CashMovementDirection.In,   "Cobro cliente - efectivo"),
            SalePaymentMethod.Transfer => (CashMovementType.TransferIncome,        CashMovementDirection.In,   "Cobro cliente - transferencia"),
            SalePaymentMethod.Card     => (CashMovementType.CardIncome,            CashMovementDirection.In,   "Cobro cliente - tarjeta"),
            SalePaymentMethod.Check    => (CashMovementType.CuentaCorrienteIncome, CashMovementDirection.None, "Cobro cliente - cheque"),
            _                          => (CashMovementType.CuentaCorrienteIncome, CashMovementDirection.None, "Cobro cliente - otros"),
        };

        AddMovement(
            type,
            direction,
            amount,
            CashReferenceTypes.CustomerPayment,
            customerPaymentId,
            description,
            createdByUserId,
            paymentMethod: (int)method,
            customerPaymentId: customerPaymentId);
    }

    // Reversa de un cobro a nivel cliente, por método: el efectivo sale del cajón (Out) y
    // transferencia/tarjeta/cheque/otros se registran como reversa neutra (None) para trazabilidad
    // sin tocar el efectivo esperado — espejo de RegisterCcPaymentCancellation.
    public void RegisterCustomerPaymentCancellation(
        SalePaymentMethod method,
        decimal amount,
        Guid customerPaymentId,
        UserId createdByUserId)
    {
        EnsureOpen();

        var direction = method == SalePaymentMethod.Cash
            ? CashMovementDirection.Out
            : CashMovementDirection.None;

        var description = method switch
        {
            SalePaymentMethod.Cash     => "Cobro cliente anulado - efectivo",
            SalePaymentMethod.Transfer => "Cobro cliente anulado - transferencia",
            SalePaymentMethod.Card     => "Cobro cliente anulado - tarjeta",
            SalePaymentMethod.Check    => "Cobro cliente anulado - cheque",
            _                          => "Cobro cliente anulado - otros",
        };

        AddMovement(
            CashMovementType.CuentaCorrienteCancellation,
            direction,
            amount,
            CashReferenceTypes.CustomerPayment,
            customerPaymentId,
            description,
            createdByUserId,
            paymentMethod: (int)method,
            customerPaymentId: customerPaymentId);
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
            originalCashSessionId,
            paymentMethod: (int)payment.Method);
        }
    }

    public void RegisterSupplierPaymentExpense(
        decimal amount,
        Guid supplierPaymentId,
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
            CashReferenceTypes.SupplierPayment,
            supplierPaymentId,
            methodName,
            createdByUserId,
            paymentMethod: (int)method,
            supplierPaymentId: supplierPaymentId);
    }

    public void RegisterSupplierPaymentCancel(
        decimal amount,
        Guid supplierPaymentId,
        UserId createdByUserId,
        Domain.Purchases.PurchasePaymentMethod method = Domain.Purchases.PurchasePaymentMethod.Cash)
    {
        EnsureOpen();
        var direction = method == Domain.Purchases.PurchasePaymentMethod.Cash
            ? CashMovementDirection.In
            : CashMovementDirection.None;
        AddMovement(
            CashMovementType.PurchasePaymentCancellation,
            direction,
            amount,
            CashReferenceTypes.SupplierPayment,
            supplierPaymentId,
            "Pago de compra anulado",
            createdByUserId,
            paymentMethod: (int)method,
            supplierPaymentId: supplierPaymentId);
    }

    public void RegisterPayrollExpense(decimal amount, Guid payrollLiquidationId, UserId createdByUserId)
    {
        EnsureOpen();

        if (ExpectedClosingAmount - amount < 0)
        {
            throw new InvalidOperationException("Payroll payment cannot leave a negative expected balance.");
        }

        AddMovement(
            CashMovementType.PayrollExpense,
            CashMovementDirection.Out,
            amount,
            CashReferenceTypes.PayrollLiquidation,
            payrollLiquidationId,
            "Pago de sueldo",
            createdByUserId,
            payrollLiquidationId: payrollLiquidationId);
    }

    public void RegisterPayrollExpenseCancel(decimal amount, Guid payrollLiquidationId, UserId createdByUserId)
    {
        EnsureOpen();

        AddMovement(
            CashMovementType.PayrollExpenseCancellation,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.PayrollLiquidation,
            payrollLiquidationId,
            "Pago de sueldo anulado",
            createdByUserId,
            payrollLiquidationId: payrollLiquidationId);
    }

    public void RegisterPayrollAdvanceExpense(decimal amount, Guid payrollAdvanceId, UserId createdByUserId)
    {
        EnsureOpen();

        if (ExpectedClosingAmount - amount < 0)
        {
            throw new InvalidOperationException("Payroll advance cannot leave a negative expected balance.");
        }

        AddMovement(
            CashMovementType.PayrollAdvanceExpense,
            CashMovementDirection.Out,
            amount,
            CashReferenceTypes.PayrollAdvance,
            payrollAdvanceId,
            "Adelanto de sueldo",
            createdByUserId,
            payrollAdvanceId: payrollAdvanceId);
    }

    public void RegisterPayrollAdvanceExpenseCancel(decimal amount, Guid payrollAdvanceId, UserId createdByUserId)
    {
        EnsureOpen();

        AddMovement(
            CashMovementType.PayrollAdvanceExpenseCancellation,
            CashMovementDirection.In,
            amount,
            CashReferenceTypes.PayrollAdvance,
            payrollAdvanceId,
            "Adelanto de sueldo anulado",
            createdByUserId,
            payrollAdvanceId: payrollAdvanceId);
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
        Guid? ccPaymentGroupId = null,
        int? paymentMethod = null,
        Guid? saleCcPaymentId = null,
        Guid? supplierPaymentId = null,
        Guid? customerPaymentId = null,
        Guid? payrollLiquidationId = null,
        Guid? payrollAdvanceId = null)
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
            originalCashSessionId: originalCashSessionId,
            paymentMethod: paymentMethod,
            saleCcPaymentId: saleCcPaymentId,
            supplierPaymentId: supplierPaymentId,
            customerPaymentId: customerPaymentId,
            payrollLiquidationId: payrollLiquidationId,
            payrollAdvanceId: payrollAdvanceId));
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
