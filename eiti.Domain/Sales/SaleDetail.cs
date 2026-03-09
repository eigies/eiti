using eiti.Domain.Products;

namespace eiti.Domain.Sales;

public sealed class SaleDetail
{
    public SaleId SaleId { get; private set; }
    public ProductId ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TotalAmount { get; private set; }

    private SaleDetail()
    {
    }

    private SaleDetail(
        ProductId productId,
        int quantity,
        decimal unitPrice)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Sale detail quantity must be greater than zero.", nameof(quantity));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentException("Sale detail unit price cannot be negative.", nameof(unitPrice));
        }

        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TotalAmount = quantity * unitPrice;
    }

    public static SaleDetail Create(
        ProductId productId,
        int quantity,
        decimal unitPrice)
    {
        return new SaleDetail(productId, quantity, unitPrice);
    }

    internal void AttachToSale(SaleId saleId)
    {
        SaleId = saleId;
    }
}
