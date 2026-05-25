namespace eiti.Domain.Purchases;

public sealed class PurchaseDetail
{
    public Guid Id { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal TotalAmount => Quantity * UnitCost;

    private PurchaseDetail()
    {
    }

    private PurchaseDetail(Guid id, Guid productId, string productName, decimal quantity, decimal unitCost)
    {
        if (quantity <= 0)
            throw new ArgumentException("Purchase detail quantity must be greater than zero.", nameof(quantity));

        if (unitCost < 0)
            throw new ArgumentException("Purchase detail unit cost cannot be negative.", nameof(unitCost));

        Id = id;
        ProductId = productId;
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitCost = decimal.Round(unitCost, 2, MidpointRounding.AwayFromZero);
    }

    public static PurchaseDetail Create(Guid productId, string productName, decimal quantity, decimal unitCost)
    {
        return new PurchaseDetail(Guid.NewGuid(), productId, productName, quantity, unitCost);
    }

    internal void AttachToPurchase(Guid purchaseId)
    {
        PurchaseId = purchaseId;
    }
}
