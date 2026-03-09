using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Primitives;
using eiti.Domain.Transport;

namespace eiti.Domain.Sales;

public sealed class Sale : AggregateRoot<SaleId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public BranchId BranchId { get; private set; } = null!;
    public CustomerId? CustomerId { get; private set; }
    public CashSessionId? CashSessionId { get; private set; }
    public bool HasDelivery { get; private set; }
    public SaleTransportAssignmentId? TransportAssignmentId { get; private set; }
    public SaleStatus SaleStatus { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsModified { get; private set; }
    private readonly List<SaleDetail> _details = [];
    public IReadOnlyCollection<SaleDetail> Details => _details;

    private Sale()
    {
    }

    private Sale(
        SaleId id,
        CompanyId companyId,
        BranchId branchId,
        CustomerId? customerId,
        bool hasDelivery,
        SaleStatus saleStatus,
        DateTime createdAt,
        List<SaleDetail> details)
        : base(id)
    {
        CompanyId = companyId;
        BranchId = branchId;
        CustomerId = customerId;
        HasDelivery = hasDelivery;
        SaleStatus = saleStatus;
        CreatedAt = createdAt;
        _details = details;

        foreach (var detail in _details)
        {
            detail.AttachToSale(id);
        }

        TotalAmount = _details.Sum(detail => detail.TotalAmount);
    }

    public static Sale Create(
        CompanyId companyId,
        BranchId branchId,
        CustomerId? customerId,
        bool hasDelivery,
        SaleStatus saleStatus,
        IEnumerable<SaleDetail> details)
    {
        if (saleStatus == SaleStatus.Cancel)
        {
            throw new ArgumentException("A sale cannot be created with Cancel status.", nameof(saleStatus));
        }

        var detailList = details.ToList();

        if (detailList.Count == 0)
        {
            throw new ArgumentException("A sale must contain at least one detail.", nameof(details));
        }

        return new Sale(
            SaleId.New(),
            companyId,
            branchId,
            customerId,
            hasDelivery,
            saleStatus,
            DateTime.UtcNow,
            detailList);
    }

    public void Update(
        CustomerId? customerId,
        SaleStatus saleStatus,
        bool hasDelivery,
        IEnumerable<SaleDetail> details)
    {
        if (SaleStatus != SaleStatus.OnHold)
        {
            throw new InvalidOperationException("Only sales in OnHold status can be modified.");
        }

        var detailList = details.ToList();

        if (detailList.Count == 0)
        {
            throw new ArgumentException("A sale must contain at least one detail.", nameof(details));
        }

        if (saleStatus == SaleStatus.Paid)
        {
            throw new InvalidOperationException("A sale cannot be marked as paid through the generic update operation.");
        }

        if (!hasDelivery)
        {
            TransportAssignmentId = null;
        }

        _details.Clear();

        foreach (var detail in detailList)
        {
            detail.AttachToSale(Id);
            _details.Add(detail);
        }

        CustomerId = customerId;
        HasDelivery = hasDelivery;
        SaleStatus = saleStatus;
        TotalAmount = _details.Sum(detail => detail.TotalAmount);
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    public void MarkAsPaid(CashSessionId cashSessionId)
    {
        if (SaleStatus == SaleStatus.Paid)
        {
            throw new InvalidOperationException("The sale is already paid.");
        }

        if (SaleStatus != SaleStatus.OnHold)
        {
            throw new InvalidOperationException("Only sales in OnHold status can be marked as paid.");
        }

        CashSessionId = cashSessionId;
        SaleStatus = SaleStatus.Paid;
        PaidAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    public void AssignTransport(SaleTransportAssignmentId transportAssignmentId)
    {
        if (!HasDelivery)
        {
            throw new InvalidOperationException("This sale is not marked for delivery.");
        }

        TransportAssignmentId = transportAssignmentId;
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    public void ClearTransportAssignment()
    {
        TransportAssignmentId = null;
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }
}
