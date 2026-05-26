namespace eiti.Application.Features.Purchases.Queries.ListPurchases;

public sealed record ListPurchasesResponse(
    List<ListPurchasesItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record ListPurchasesItemResponse(
    Guid Id,
    Guid? SupplierId,
    string? SupplierName,
    string? InvoiceNumber,
    int Status,
    string StatusName,
    decimal TotalAmount,
    decimal TaxAmount,
    decimal GrandTotal,
    decimal TotalPaid,
    decimal PendingAmount,
    DateTime CreatedAt,
    int DetailCount);
