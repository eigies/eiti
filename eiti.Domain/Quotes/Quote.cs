using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Primitives;

namespace eiti.Domain.Quotes;

public sealed class Quote : AggregateRoot<QuoteId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public BranchId BranchId { get; private set; } = null!;
    public CustomerId? CustomerId { get; private set; }
    public string? ProspectName { get; private set; }
    public string? ProspectContact { get; private set; }
    public decimal GeneralDiscountPercent { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public QuoteStatus Status { get; private set; }
    public Guid? ConvertedSaleId { get; private set; }
    public string? Code { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<QuoteDetail> _details = [];
    public IReadOnlyCollection<QuoteDetail> Details => _details;

    private Quote()
    {
    }

    private Quote(
        QuoteId id,
        CompanyId companyId,
        BranchId branchId,
        CustomerId? customerId,
        string? prospectName,
        string? prospectContact,
        List<QuoteDetail> details,
        decimal generalDiscountPercent,
        DateTime expiresAt,
        Guid createdByUserId,
        DateTime createdAt,
        string? code)
        : base(id)
    {
        CompanyId = companyId;
        BranchId = branchId;
        CustomerId = customerId;
        ProspectName = prospectName;
        ProspectContact = prospectContact;
        GeneralDiscountPercent = NormalizePercent(generalDiscountPercent);
        ExpiresAt = expiresAt;
        Status = QuoteStatus.Pending;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        Code = code;
        _details = details;

        foreach (var detail in _details)
        {
            detail.AttachToQuote(id);
        }

        RecalculateTotal();
    }

    public static Quote Create(
        CompanyId companyId,
        BranchId branchId,
        CustomerId? customerId,
        string? prospectName,
        string? prospectContact,
        IEnumerable<QuoteDetail> details,
        decimal generalDiscountPercent,
        DateTime expiresAt,
        Guid createdByUserId,
        string? code = null,
        DateTime? createdAt = null)
    {
        var hasCustomer = customerId is not null;
        var hasProspect = !string.IsNullOrWhiteSpace(prospectName);

        if (hasCustomer == hasProspect)
        {
            throw new ArgumentException(
                "A quote must have exactly one of CustomerId or ProspectName.", nameof(customerId));
        }

        var detailList = details.ToList();
        if (detailList.Count == 0)
        {
            throw new ArgumentException("A quote requires at least one detail.", nameof(details));
        }

        var effectiveCreatedAt = createdAt ?? DateTime.UtcNow;
        if (expiresAt <= effectiveCreatedAt)
        {
            throw new ArgumentException("ExpiresAt must be in the future.", nameof(expiresAt));
        }

        return new Quote(
            QuoteId.New(),
            companyId,
            branchId,
            customerId,
            hasProspect ? prospectName!.Trim() : null,
            hasProspect ? prospectContact?.Trim() : null,
            detailList,
            generalDiscountPercent,
            expiresAt,
            createdByUserId,
            effectiveCreatedAt,
            code);
    }

    public bool IsExpired(DateTime now) => Status == QuoteStatus.Pending && ExpiresAt < now;

    public void Cancel()
    {
        if (Status != QuoteStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot cancel a quote in status '{Status}'.");
        }

        Status = QuoteStatus.Cancelled;
    }

    public void MarkConverted(Guid saleId, DateTime now)
    {
        if (Status != QuoteStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot convert a quote in status '{Status}'.");
        }

        if (IsExpired(now))
        {
            throw new InvalidOperationException("Cannot convert an expired quote.");
        }

        Status = QuoteStatus.Converted;
        ConvertedSaleId = saleId;
    }

    private void RecalculateTotal()
    {
        var subtotal = _details.Sum(detail => detail.LineTotal);
        TotalAmount = GeneralDiscountPercent > 0
            ? decimal.Round(subtotal * (1m - GeneralDiscountPercent / 100m), 2, MidpointRounding.AwayFromZero)
            : decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal NormalizePercent(decimal value)
    {
        if (value < 0 || value > 100)
        {
            throw new ArgumentException("Discount percent must be between 0 and 100.", nameof(value));
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
