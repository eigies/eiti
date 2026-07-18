using eiti.Domain.Products;

namespace eiti.Domain.Quotes;

public sealed class QuoteDetail
{
    public QuoteId QuoteId { get; private set; } = null!;
    public ProductId ProductId { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal LineTotal { get; private set; }

    private QuoteDetail()
    {
    }

    private QuoteDetail(ProductId productId, int quantity, decimal unitPrice, decimal discountPercent)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Quote detail quantity must be greater than zero.", nameof(quantity));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentException("Quote detail unit price cannot be negative.", nameof(unitPrice));
        }

        if (discountPercent < 0 || discountPercent > 100)
        {
            throw new ArgumentException("Discount percent must be between 0 and 100.", nameof(discountPercent));
        }

        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountPercent = decimal.Round(discountPercent, 2, MidpointRounding.AwayFromZero);
        LineTotal = ComputeTotal(quantity, unitPrice, DiscountPercent);
    }

    public static QuoteDetail Create(ProductId productId, int quantity, decimal unitPrice, decimal discountPercent = 0)
    {
        return new QuoteDetail(productId, quantity, unitPrice, discountPercent);
    }

    internal void AttachToQuote(QuoteId quoteId)
    {
        QuoteId = quoteId;
    }

    private static decimal ComputeTotal(int quantity, decimal unitPrice, decimal discountPercent)
    {
        var subtotal = quantity * unitPrice;
        if (discountPercent > 0)
        {
            subtotal *= 1m - discountPercent / 100m;
        }
        return decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero);
    }
}
