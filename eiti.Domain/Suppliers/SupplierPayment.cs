using eiti.Domain.Purchases;

namespace eiti.Domain.Suppliers;

// Pago real al proveedor (el "crédito" de la cuenta corriente / bolsa). Se imputa FIFO a las compras
// pendientes del proveedor; el excedente queda como saldo a favor. Lleva el método/cheque/caja real.
public sealed class SupplierPayment
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid BranchId { get; private set; }
    public PurchasePaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public PurchasePaymentStatus Status { get; private set; }
    public Guid? ChequeId { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public DateTime Date { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private SupplierPayment()
    {
    }

    private SupplierPayment(
        Guid id,
        Guid companyId,
        Guid supplierId,
        Guid branchId,
        PurchasePaymentMethod method,
        decimal amount,
        DateTime date,
        string? reference,
        string? notes,
        Guid? chequeId,
        Guid createdByUserId)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        Id = id;
        CompanyId = companyId;
        SupplierId = supplierId;
        BranchId = branchId;
        Method = method;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Status = PurchasePaymentStatus.Active;
        ChequeId = chequeId;
        Date = date;
        Reference = NormalizeOptional(reference, 120);
        Notes = NormalizeOptional(notes, 500);
        CreatedAt = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
    }

    public static SupplierPayment Create(
        Guid companyId,
        Guid supplierId,
        Guid branchId,
        PurchasePaymentMethod method,
        decimal amount,
        DateTime date,
        string? reference,
        string? notes,
        Guid createdByUserId,
        Guid? chequeId = null)
    {
        return new SupplierPayment(
            Guid.NewGuid(), companyId, supplierId, branchId, method, amount, date, reference, notes, chequeId, createdByUserId);
    }

    public void Cancel()
    {
        if (Status == PurchasePaymentStatus.Cancelled)
            throw new InvalidOperationException("Supplier payment is already cancelled.");

        Status = PurchasePaymentStatus.Cancelled;
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
