namespace eiti.Application.Features.Quotes.Commands.CreateQuote;

public sealed record CreateQuoteResponse(
    Guid Id,
    string? Code,
    Guid BranchId,
    Guid? CustomerId,
    string? CustomerFullName,
    string? ProspectName,
    string? ProspectContact,
    decimal GeneralDiscountPercent,
    decimal TotalAmount,
    decimal VatRate,
    bool IncludesVat,
    decimal NetAmount,
    decimal VatAmount,
    decimal GrandTotal,
    DateTime ExpiresAt,
    int IdQuoteStatus,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<CreateQuoteDetailItemResponse> Details);

public sealed record CreateQuoteDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal LineTotal);
