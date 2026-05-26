namespace eiti.Domain.Purchases;

public sealed class PurchasePayment
{
    public Guid Id { get; private set; }
    public Guid PurchaseId { get; private set; }
    public PurchasePaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public PurchasePaymentStatus Status { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public DateTime Date { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public decimal? IvaPct { get; private set; }
    public decimal? IngresosBrutosPct { get; private set; }

    private PurchasePayment()
    {
    }

    private PurchasePayment(
        Guid id,
        PurchasePaymentMethod method,
        decimal amount,
        DateTime date,
        string? reference,
        string? notes,
        decimal? ivaPct,
        decimal? ingresosBrutosPct)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        Id = id;
        Method = method;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Status = PurchasePaymentStatus.Active;
        Date = date;
        Reference = NormalizeOptional(reference, 120);
        Notes = NormalizeOptional(notes, 500);
        CreatedAt = DateTime.UtcNow;
        IvaPct = ivaPct;
        IngresosBrutosPct = ingresosBrutosPct;
    }

    public static PurchasePayment Create(
        PurchasePaymentMethod method,
        decimal amount,
        DateTime date,
        string? reference,
        string? notes,
        decimal? ivaPct = null,
        decimal? ingresosBrutosPct = null)
    {
        return new PurchasePayment(Guid.NewGuid(), method, amount, date, reference, notes, ivaPct, ingresosBrutosPct);
    }

    public void Cancel()
    {
        if (Status == PurchasePaymentStatus.Cancelled)
            throw new InvalidOperationException("Payment is already cancelled.");

        Status = PurchasePaymentStatus.Cancelled;
    }

    internal void AttachToPurchase(Guid purchaseId)
    {
        PurchaseId = purchaseId;
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
