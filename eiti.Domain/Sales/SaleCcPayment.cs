using eiti.Domain.Primitives;

namespace eiti.Domain.Sales;

public sealed class SaleCcPayment : Entity<SaleCcPaymentId>
{
    public SaleId SaleId { get; private set; } = null!;
    public SalePaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime Date { get; private set; }
    public string? Notes { get; private set; }
    public SaleCcPaymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    private SaleCcPayment()
    {
    }

    private SaleCcPayment(
        SaleCcPaymentId id,
        SaleId saleId,
        SalePaymentMethod method,
        decimal amount,
        DateTime date,
        string? notes)
        : base(id)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));
        }

        if (!Enum.IsDefined(typeof(SalePaymentMethod), method))
        {
            throw new ArgumentException("The selected payment method is invalid.", nameof(method));
        }

        SaleId = saleId;
        Method = method;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Date = date;
        Notes = NormalizeOptional(notes, 250, nameof(notes));
        Status = SaleCcPaymentStatus.Active;
        CreatedAt = DateTime.UtcNow;
    }

    public static SaleCcPayment Create(
        SaleId saleId,
        SalePaymentMethod method,
        decimal amount,
        DateTime date,
        string? notes)
    {
        return new SaleCcPayment(SaleCcPaymentId.New(), saleId, method, amount, date, notes);
    }

    public void Cancel()
    {
        if (Status == SaleCcPaymentStatus.Cancelled)
        {
            throw new InvalidOperationException("The payment is already cancelled.");
        }

        Status = SaleCcPaymentStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"The value cannot exceed {maxLength} characters.", paramName);
        }

        return normalized;
    }
}
