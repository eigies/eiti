namespace eiti.Domain.Sales;

public sealed class SalePayment
{
    public SaleId SaleId { get; private set; } = null!;
    public SalePaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }

    private SalePayment()
    {
    }

    private SalePayment(
        SalePaymentMethod method,
        decimal amount,
        string? reference)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));
        }

        Method = method;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Reference = NormalizeOptional(reference, 120, nameof(reference));
    }

    public static SalePayment Create(
        SalePaymentMethod method,
        decimal amount,
        string? reference)
    {
        return new SalePayment(method, amount, reference);
    }

    internal void AttachToSale(SaleId saleId)
    {
        SaleId = saleId;
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
