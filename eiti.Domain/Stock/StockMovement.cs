using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Primitives;
using eiti.Domain.Products;
using eiti.Domain.Users;

namespace eiti.Domain.Stock;

public sealed class StockMovement : Entity<StockMovementId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public BranchId BranchId { get; private set; } = null!;
    public ProductId ProductId { get; private set; } = null!;
    public BranchProductStockId BranchProductStockId { get; private set; } = null!;
    public StockMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public UserId? CreatedByUserId { get; private set; }

    private StockMovement()
    {
    }

    private StockMovement(
        StockMovementId id,
        CompanyId companyId,
        BranchId branchId,
        ProductId productId,
        BranchProductStockId branchProductStockId,
        StockMovementType type,
        int quantity,
        string? referenceType,
        Guid? referenceId,
        string? description,
        UserId? createdByUserId)
        : base(id)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Stock movement quantity must be greater than zero.", nameof(quantity));
        }

        CompanyId = companyId;
        BranchId = branchId;
        ProductId = productId;
        BranchProductStockId = branchProductStockId;
        Type = type;
        Quantity = quantity;
        ReferenceType = NormalizeOptional(referenceType, 50);
        ReferenceId = referenceId;
        Description = NormalizeOptional(description, 255);
        CreatedAt = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
    }

    public static StockMovement Create(
        CompanyId companyId,
        BranchId branchId,
        ProductId productId,
        BranchProductStockId branchProductStockId,
        StockMovementType type,
        int quantity,
        string? referenceType,
        Guid? referenceId,
        string? description,
        UserId? createdByUserId)
    {
        return new StockMovement(
            StockMovementId.New(),
            companyId,
            branchId,
            productId,
            branchProductStockId,
            type,
            quantity,
            referenceType,
            referenceId,
            description,
            createdByUserId);
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"The value cannot exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }
}
