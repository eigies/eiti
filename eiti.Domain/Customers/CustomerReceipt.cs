using eiti.Domain.Sales;

namespace eiti.Domain.Customers;

// Recibo (RC-0001): agrupa en un solo comprobante los cobros que un cliente hace juntos (tarjeta +
// cheques + efectivo). Cada medio sigue siendo un CustomerPayment completo, con su caja, su cheque,
// su recargo de tarjeta y su imputación FIFO; el recibo solo los numera, los suma y se anula entero.
// Espejo de SupplierPaymentOrder. Los cobros anteriores a los recibos quedan con ReceiptId en null.
public sealed class CustomerReceipt
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public DateTime Date { get; private set; }
    public string? Notes { get; private set; }
    public decimal TotalAmount { get; private set; }
    public SaleCcPaymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledByUserId { get; private set; }

    private CustomerReceipt()
    {
    }

    private CustomerReceipt(
        Guid id,
        Guid companyId,
        Guid customerId,
        Guid branchId,
        string code,
        DateTime date,
        string? notes,
        Guid createdByUserId)
    {
        Id = id;
        CompanyId = companyId;
        CustomerId = customerId;
        BranchId = branchId;
        Code = NormalizeRequired(code, 20, nameof(code));
        Date = date;
        Notes = NormalizeOptional(notes, 500);
        TotalAmount = 0m;
        Status = SaleCcPaymentStatus.Active;
        CreatedAt = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
    }

    public static CustomerReceipt Create(
        Guid companyId,
        Guid customerId,
        Guid branchId,
        string code,
        DateTime date,
        string? notes,
        Guid createdByUserId)
    {
        return new CustomerReceipt(
            Guid.NewGuid(), companyId, customerId, branchId, code, date, notes, createdByUserId);
    }

    // Suma un medio al recibo: el cobro queda apuntando al recibo y el total se recalcula.
    // El total es lo imputado a la cuenta (Amount), no lo cobrado con recargo de tarjeta.
    public void AddPayment(CustomerPayment payment)
    {
        if (Status == SaleCcPaymentStatus.Cancelled)
            throw new InvalidOperationException("Cannot add payments to a cancelled receipt.");

        if (payment.CompanyId != CompanyId || payment.CustomerId != CustomerId)
            throw new InvalidOperationException("The payment belongs to another customer.");

        if (payment.Status != SaleCcPaymentStatus.Active)
            throw new InvalidOperationException("Only active payments can be added to a receipt.");

        payment.AttachToReceipt(Id);
        TotalAmount = decimal.Round(TotalAmount + payment.Amount, 2, MidpointRounding.AwayFromZero);
    }

    public void Cancel(Guid cancelledByUserId)
    {
        if (Status == SaleCcPaymentStatus.Cancelled)
            throw new InvalidOperationException("Receipt is already cancelled.");

        Status = SaleCcPaymentStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancelledByUserId = cancelledByUserId;
    }

    private static string NormalizeRequired(string value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException($"{paramName} cannot exceed {maxLength} characters.", paramName);

        return normalized;
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
