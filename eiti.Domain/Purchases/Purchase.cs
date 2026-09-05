namespace eiti.Domain.Purchases;

public sealed class Purchase
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid SupplierId { get; private set; }
    public PurchaseStatus Status { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public decimal? IvaPct { get; private set; }
    public decimal? IngresosBrutosPct { get; private set; }

    private readonly List<PurchaseDetail> _details = [];
    public IReadOnlyCollection<PurchaseDetail> Details => _details.AsReadOnly();

    private readonly List<PurchasePayment> _payments = [];
    public IReadOnlyCollection<PurchasePayment> Payments => _payments.AsReadOnly();

    public decimal TotalAmount => _details.Sum(d => d.TotalAmount);
    public decimal TaxAmount => NormalizeAmount(TotalAmount * ((IvaPct ?? 0m) + (IngresosBrutosPct ?? 0m)) / 100m);
    public decimal GrandTotal => TotalAmount + TaxAmount;
    public decimal TotalPaid => _payments.Where(p => p.Status == PurchasePaymentStatus.Active).Sum(p => p.Amount);
    public decimal PendingAmount => NormalizeAmount(GrandTotal - TotalPaid);

    private Purchase()
    {
    }

    public static Purchase Create(
        Guid companyId,
        Guid branchId,
        Guid supplierId,
        IReadOnlyList<PurchaseDetail> details,
        string? invoiceNumber,
        string? notes,
        Guid createdByUserId,
        string code,
        decimal? ivaPct = null,
        decimal? ingresosBrutosPct = null)
    {
        if (details.Count == 0)
            throw new ArgumentException("A purchase must contain at least one detail.", nameof(details));

        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            Code = code,
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId,
            Status = PurchaseStatus.Active,
            InvoiceNumber = NormalizeOptional(invoiceNumber, 100),
            Notes = NormalizeOptional(notes, 500),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
            IvaPct = ivaPct,
            IngresosBrutosPct = ingresosBrutosPct
        };

        foreach (var detail in details)
        {
            detail.AttachToPurchase(purchase.Id);
            purchase._details.Add(detail);
        }

        return purchase;
    }

    public void AddPayment(PurchasePayment payment)
    {
        payment.AttachToPurchase(Id);
        _payments.Add(payment);

        if (NormalizeAmount(PendingAmount) <= 0 && Status == PurchaseStatus.Active)
        {
            Status = PurchaseStatus.Paid;
            PaidAt = DateTime.UtcNow;
        }
    }

    public void CancelPayment(Guid paymentId)
    {
        var payment = _payments.Single(p => p.Id == paymentId);
        payment.Cancel();

        if (Status == PurchaseStatus.Paid && NormalizeAmount(PendingAmount) > 0)
        {
            Status = PurchaseStatus.Active;
            PaidAt = null;
        }
    }

    // Cancela las imputaciones que generó una nota de crédito y recalcula el estado.
    // Filtra por CreditNoteId para no tocar las de un pago ni las de otra NC.
    // Devuelve el total desimputado, para que el caller sepa cuánto crédito revertir.
    public decimal RevertCreditNote(Guid creditNoteId)
    {
        var rows = _payments
            .Where(p => p.CreditNoteId == creditNoteId && p.Status == PurchasePaymentStatus.Active)
            .ToList();

        var total = 0m;
        foreach (var row in rows)
        {
            total += row.Amount;
            CancelPayment(row.Id);
        }

        return total;
    }

    public void Cancel()
    {
        if (Status == PurchaseStatus.Cancelled)
            throw new InvalidOperationException("Purchase is already cancelled.");

        Status = PurchaseStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
    }

    private static decimal NormalizeAmount(decimal amount)
    {
        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
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
