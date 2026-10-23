using eiti.Domain.Purchases;

namespace eiti.Domain.Suppliers;

// Orden de pago (OP-0001): agrupa en un solo comprobante los pagos que se le hacen juntos a un
// proveedor (varios cheques + transferencia + efectivo). Cada medio sigue siendo un SupplierPayment
// completo, con su caja, su cheque y su imputación FIFO; la orden solo los numera, los suma y se
// anula entera. Los pagos anteriores a la orden quedan con PaymentOrderId en null.
public sealed class SupplierPaymentOrder
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public DateTime Date { get; private set; }
    public string? Notes { get; private set; }
    public decimal TotalAmount { get; private set; }
    public PurchasePaymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledByUserId { get; private set; }

    private SupplierPaymentOrder()
    {
    }

    private SupplierPaymentOrder(
        Guid id,
        Guid companyId,
        Guid supplierId,
        Guid branchId,
        string code,
        DateTime date,
        string? notes,
        Guid createdByUserId)
    {
        Id = id;
        CompanyId = companyId;
        SupplierId = supplierId;
        BranchId = branchId;
        Code = NormalizeRequired(code, 20, nameof(code));
        Date = date;
        Notes = NormalizeOptional(notes, 500);
        TotalAmount = 0m;
        Status = PurchasePaymentStatus.Active;
        CreatedAt = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
    }

    public static SupplierPaymentOrder Create(
        Guid companyId,
        Guid supplierId,
        Guid branchId,
        string code,
        DateTime date,
        string? notes,
        Guid createdByUserId)
    {
        return new SupplierPaymentOrder(
            Guid.NewGuid(), companyId, supplierId, branchId, code, date, notes, createdByUserId);
    }

    // Suma un medio a la orden: el pago queda apuntando a la orden y el total se recalcula.
    public void AddPayment(SupplierPayment payment)
    {
        if (Status == PurchasePaymentStatus.Cancelled)
            throw new InvalidOperationException("Cannot add payments to a cancelled payment order.");

        if (payment.CompanyId != CompanyId || payment.SupplierId != SupplierId)
            throw new InvalidOperationException("The payment belongs to another supplier.");

        if (payment.Status != PurchasePaymentStatus.Active)
            throw new InvalidOperationException("Only active payments can be added to a payment order.");

        payment.AttachToOrder(Id);
        TotalAmount = decimal.Round(TotalAmount + payment.Amount, 2, MidpointRounding.AwayFromZero);
    }

    public void Cancel(Guid cancelledByUserId)
    {
        if (Status == PurchasePaymentStatus.Cancelled)
            throw new InvalidOperationException("Payment order is already cancelled.");

        Status = PurchasePaymentStatus.Cancelled;
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
