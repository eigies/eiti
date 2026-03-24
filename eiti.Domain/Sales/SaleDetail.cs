using eiti.Domain.Products;

namespace eiti.Domain.Sales;

public sealed class SaleDetail
{
    public SaleId SaleId { get; private set; }
    public ProductId ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal TotalAmount { get; private set; }

    private SaleDetail()
    {
    }

    private SaleDetail(
        ProductId productId,
        int quantity,
        decimal unitPrice,
        decimal discountPercent)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Sale detail quantity must be greater than zero.", nameof(quantity));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentException("Sale detail unit price cannot be negative.", nameof(unitPrice));
        }

        if (discountPercent < 0 || discountPercent > 100)
        {
            throw new ArgumentException("Discount percent must be between 0 and 100.", nameof(discountPercent));
        }

        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountPercent = decimal.Round(discountPercent, 2, MidpointRounding.AwayFromZero);
        TotalAmount = ComputeTotal(quantity, unitPrice, DiscountPercent);
    }

    public static SaleDetail Create(
        ProductId productId,
        int quantity,
        decimal unitPrice,
        decimal discountPercent = 0)
    {
        return new SaleDetail(productId, quantity, unitPrice, discountPercent);
    }

    internal void AttachToSale(SaleId saleId)
    {
        SaleId = saleId;
    }

    private static decimal ComputeTotal(int quantity, decimal unitPrice, decimal discountPercent)
    {
        var lineSubtotal = quantity * unitPrice;
        if (discountPercent > 0)
        {
            lineSubtotal = lineSubtotal * (1m - discountPercent / 100m);
        }
        return decimal.Round(lineSubtotal, 2, MidpointRounding.AwayFromZero);
    }
}
