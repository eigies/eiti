using eiti.Domain.Products;

namespace eiti.Domain.Sales;

public sealed class SaleTradeIn
{
    public SaleId SaleId { get; private set; } = null!;
    public ProductId ProductId { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal Amount { get; private set; }

    private SaleTradeIn()
    {
    }

    private SaleTradeIn(
        ProductId productId,
        int quantity,
        decimal amount)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Trade-in quantity must be greater than zero.", nameof(quantity));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Trade-in amount must be greater than zero.", nameof(amount));
        }

        ProductId = productId;
        Quantity = quantity;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static SaleTradeIn Create(
        ProductId productId,
        int quantity,
        decimal amount)
    {
        return new SaleTradeIn(productId, quantity, amount);
    }

    internal void AttachToSale(SaleId saleId)
    {
        SaleId = saleId;
    }
}
