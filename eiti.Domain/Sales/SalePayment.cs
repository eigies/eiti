namespace eiti.Domain.Sales;

public sealed class SalePayment
{
    public SaleId SaleId { get; private set; } = null!;
    public SalePaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }
    public int? CardBankId { get; private set; }
    public int? CardCuotas { get; private set; }
    public decimal? CardSurchargePct { get; private set; }
    public decimal? CardSurchargeAmt { get; private set; }
    public decimal? TotalCobrado { get; private set; }

    private SalePayment()
    {
    }

    private SalePayment(
        SalePaymentMethod method,
        decimal amount,
        string? reference,
        int? cardBankId = null,
        int? cardCuotas = null,
        decimal? cardSurchargePct = null,
        decimal? cardSurchargeAmt = null,
        decimal? totalCobrado = null)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));
        }

        Method = method;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Reference = NormalizeOptional(reference, 120, nameof(reference));
        CardBankId = cardBankId;
        CardCuotas = cardCuotas;
        CardSurchargePct = cardSurchargePct;
        CardSurchargeAmt = cardSurchargeAmt;
        TotalCobrado = totalCobrado;
    }

    public static SalePayment Create(
        SalePaymentMethod method,
        decimal amount,
        string? reference,
        int? cardBankId = null,
        int? cardCuotas = null,
        decimal? cardSurchargePct = null,
        decimal? cardSurchargeAmt = null,
        decimal? totalCobrado = null)
    {
        return new SalePayment(method, amount, reference, cardBankId, cardCuotas, cardSurchargePct, cardSurchargeAmt, totalCobrado);
    }

    internal void AttachToSale(SaleId saleId)
    {
        SaleId = saleId;
    }

    public void SetCardData(int bankId, int cuotas, decimal surchargePct, decimal surchargeAmt)
    {
        CardBankId = bankId;
        CardCuotas = cuotas;
        CardSurchargePct = surchargePct;
        CardSurchargeAmt = surchargeAmt;
        TotalCobrado = Amount;
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
