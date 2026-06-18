using eiti.Domain.Sales;

namespace eiti.Domain.Customers;

// Cobro real al cliente (el "crédito" de la cuenta corriente / bolsa). Se imputa FIFO a las ventas CC
// pendientes del cliente; el excedente queda como saldo a favor. Lleva el método/cheque/tarjeta/caja real.
// Espejo de SupplierPayment + campos de tarjeta de SaleCcPayment + ChequeId (cheque recibido del cliente).
public sealed class CustomerPayment
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid BranchId { get; private set; }
    public SalePaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public SaleCcPaymentStatus Status { get; private set; }
    public Guid? ChequeId { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public DateTime Date { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    // Campos de tarjeta (espejo de SaleCcPayment).
    public int? CardBankId { get; private set; }
    public int? CardCuotas { get; private set; }
    public decimal? CardSurchargePct { get; private set; }
    public decimal? CardSurchargeAmt { get; private set; }
    public decimal? TotalCobrado { get; private set; }

    private CustomerPayment()
    {
    }

    private CustomerPayment(
        Guid id,
        Guid companyId,
        Guid customerId,
        Guid branchId,
        SalePaymentMethod method,
        decimal amount,
        DateTime date,
        string? reference,
        string? notes,
        Guid? chequeId,
        Guid createdByUserId)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        if (!Enum.IsDefined(typeof(SalePaymentMethod), method))
            throw new ArgumentException("The selected payment method is invalid.", nameof(method));

        Id = id;
        CompanyId = companyId;
        CustomerId = customerId;
        BranchId = branchId;
        Method = method;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Status = SaleCcPaymentStatus.Active;
        ChequeId = chequeId;
        Date = date;
        Reference = NormalizeOptional(reference, 120);
        Notes = NormalizeOptional(notes, 500);
        CreatedAt = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
    }

    public static CustomerPayment Create(
        Guid companyId,
        Guid customerId,
        Guid branchId,
        SalePaymentMethod method,
        decimal amount,
        DateTime date,
        string? reference,
        string? notes,
        Guid createdByUserId,
        Guid? chequeId = null)
    {
        return new CustomerPayment(
            Guid.NewGuid(), companyId, customerId, branchId, method, amount, date, reference, notes, chequeId, createdByUserId);
    }

    public void SetCardData(int bankId, int cuotas, decimal surchargePct, decimal surchargeAmt)
    {
        CardBankId = bankId;
        CardCuotas = cuotas;
        CardSurchargePct = surchargePct;
        CardSurchargeAmt = surchargeAmt;
        TotalCobrado = Amount + surchargeAmt;
    }

    public void SetChequeId(Guid chequeId)
    {
        ChequeId = chequeId;
    }

    public void Cancel()
    {
        if (Status == SaleCcPaymentStatus.Cancelled)
            throw new InvalidOperationException("Customer payment is already cancelled.");

        Status = SaleCcPaymentStatus.Cancelled;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException($"The value cannot exceed {maxLength} characters.");

        return normalized;
    }
}
