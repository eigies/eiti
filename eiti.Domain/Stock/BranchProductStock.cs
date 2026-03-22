using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Primitives;
using eiti.Domain.Products;

namespace eiti.Domain.Stock;

public sealed class BranchProductStock : AggregateRoot<BranchProductStockId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public BranchId BranchId { get; private set; } = null!;
    public ProductId ProductId { get; private set; } = null!;
    public int OnHandQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int AvailableQuantity => OnHandQuantity - ReservedQuantity;

    private BranchProductStock()
    {
    }

    private BranchProductStock(
        BranchProductStockId id,
        CompanyId companyId,
        BranchId branchId,
        ProductId productId)
        : base(id)
    {
        CompanyId = companyId;
        BranchId = branchId;
        ProductId = productId;
        OnHandQuantity = 0;
        ReservedQuantity = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public static BranchProductStock Create(
        CompanyId companyId,
        BranchId branchId,
        ProductId productId)
    {
        return new BranchProductStock(BranchProductStockId.New(), companyId, branchId, productId);
    }

    public void ApplyManualEntry(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Manual stock entry must be greater than zero.", nameof(quantity));
        }

        OnHandQuantity += quantity;
        Touch();
    }

    public void ApplyManualAdjustment(int quantity)
    {
        if (quantity == 0)
        {
            throw new ArgumentException("Manual stock adjustment cannot be zero.", nameof(quantity));
        }

        var nextOnHand = OnHandQuantity + quantity;
        if (nextOnHand < 0)
        {
            throw new InvalidOperationException("Stock cannot be negative.");
        }

        if (nextOnHand < ReservedQuantity)
        {
            throw new InvalidOperationException("Stock cannot be adjusted below the quantity already reserved.");
        }

        OnHandQuantity = nextOnHand;
        Touch();
    }

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Reserved quantity must be greater than zero.", nameof(quantity));
        }

        if (AvailableQuantity < quantity)
        {
            throw new InvalidOperationException("The requested quantity exceeds the available stock.");
        }

        ReservedQuantity += quantity;
        Touch();
    }

    public void ReleaseReservation(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Released quantity must be greater than zero.", nameof(quantity));
        }

        if (ReservedQuantity < quantity)
        {
            throw new InvalidOperationException("The requested quantity exceeds the reserved stock.");
        }

        ReservedQuantity -= quantity;
        Touch();
    }

    public void ConfirmSaleOut(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Confirmed quantity must be greater than zero.", nameof(quantity));
        }

        if (ReservedQuantity < quantity)
        {
            throw new InvalidOperationException("The requested quantity exceeds the reserved stock.");
        }

        if (OnHandQuantity < quantity)
        {
            throw new InvalidOperationException("The requested quantity exceeds the on-hand stock.");
        }

        ReservedQuantity -= quantity;
        OnHandQuantity -= quantity;
        Touch();
    }

    public void RevertSaleOut(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Reverted quantity must be greater than zero.", nameof(quantity));
        }

        OnHandQuantity += quantity;
        ReservedQuantity += quantity;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
