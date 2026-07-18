namespace eiti.Application.Features.Quotes.Queries.GetQuoteById;

public sealed record QuoteDetailResponse(
    Guid Id,
    string? Code,
    Guid BranchId,
    string BranchName,
    Guid? CustomerId,
    string? CustomerFullName,
    string? ProspectName,
    string? ProspectContact,
    decimal GeneralDiscountPercent,
    decimal TotalAmount,
    DateTime ExpiresAt,
    int IdQuoteStatus,
    string Status,
    bool IsExpired,
    Guid? ConvertedSaleId,
    DateTime CreatedAt,
    IReadOnlyList<QuoteDetailItemResponse> Details);

public sealed record QuoteDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal LineTotal);
