namespace eiti.Application.Features.Quotes.Queries.ListQuotes;

public sealed record QuoteListItemResponse(
    Guid Id,
    string? Code,
    Guid BranchId,
    Guid? CustomerId,
    string? CustomerFullName,
    string? ProspectName,
    decimal TotalAmount,
    DateTime ExpiresAt,
    int IdQuoteStatus,
    string Status,
    bool IsExpired,
    Guid? ConvertedSaleId,
    DateTime CreatedAt);
