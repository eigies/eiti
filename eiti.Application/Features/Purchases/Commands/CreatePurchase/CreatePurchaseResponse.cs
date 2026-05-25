namespace eiti.Application.Features.Purchases.Commands.CreatePurchase;

public sealed record CreatePurchaseResponse(
    Guid Id,
    Guid? SupplierId,
    string? SupplierName,
    string? InvoiceNumber,
    string? Notes,
    int Status,
    string StatusName,
    decimal TotalAmount,
    decimal TotalPaid,
    decimal PendingAmount,
    DateTime CreatedAt,
    List<CreatePurchaseDetailResponse> Details,
    List<CreatePurchasePaymentResponse> Payments);

public sealed record CreatePurchaseDetailResponse(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalAmount);

public sealed record CreatePurchasePaymentResponse(
    Guid Id,
    int Method,
    string MethodName,
    decimal Amount,
    string? Reference,
    string? Notes,
    DateTime Date);
