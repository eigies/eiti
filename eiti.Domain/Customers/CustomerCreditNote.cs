using eiti.Domain.Common;

namespace eiti.Domain.Customers;

// Nota de crédito emitida al cliente: ajusta el saldo sin mover mercadería (bonificación
// posterior, error de facturación, diferencia de precio acordada). Origina saldo a favor que
// se imputa FIFO a las ventas CC pendientes, igual que un cobro — pero sin dinero de por medio.
// Espejo de SupplierCreditNote.
public sealed class CustomerCreditNote
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid BranchId { get; private set; }

    public string Code { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime Date { get; private set; }

    // Venta asociada, opcional: la primera pregunta de cualquier contador es "¿por qué factura es?".
    public Guid? SaleId { get; private set; }

    public CreditNoteStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledByUserId { get; private set; }

    private CustomerCreditNote()
    {
    }

    private CustomerCreditNote(
        Guid id,
        Guid companyId,
        Guid customerId,
        Guid branchId,
        string code,
        decimal amount,
        string reason,
        DateTime date,
        Guid? saleId,
        Guid createdByUserId)
    {
        if (amount <= 0)
            throw new ArgumentException("Credit note amount must be greater than zero.", nameof(amount));

        Id = id;
        CompanyId = companyId;
        CustomerId = customerId;
        BranchId = branchId;
        Code = NormalizeRequired(code, 20, nameof(code));
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Reason = NormalizeRequired(reason, 250, nameof(reason));
        Date = date;
        SaleId = saleId;
        Status = CreditNoteStatus.Active;
        CreatedAt = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
    }

    public static CustomerCreditNote Create(
        Guid companyId,
        Guid customerId,
        Guid branchId,
        string code,
        decimal amount,
        string reason,
        DateTime date,
        Guid? saleId,
        Guid createdByUserId)
    {
        return new CustomerCreditNote(
            Guid.NewGuid(), companyId, customerId, branchId, code, amount, reason, date, saleId, createdByUserId);
    }

    public void Cancel(Guid cancelledByUserId)
    {
        if (Status == CreditNoteStatus.Cancelled)
            throw new InvalidOperationException("Credit note is already cancelled.");

        Status = CreditNoteStatus.Cancelled;
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
}
