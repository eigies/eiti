using eiti.Domain.Purchases;

namespace eiti.Application.Features.Purchases.Queries.GetPurchaseById;

public sealed record GetPurchaseByIdResponse(
    Guid Id,
    Guid BranchId,
    Guid? SupplierId,
    string? SupplierName,
    string? SupplierPhone,
    string? SupplierEmail,
    string? InvoiceNumber,
    string? Notes,
    int Status,
    string StatusName,
    decimal TotalAmount,
    decimal TotalPaid,
    decimal PendingAmount,
    DateTime CreatedAt,
    Guid CreatedByUserId,
    DateTime? PaidAt,
    DateTime? CancelledAt,
    List<GetPurchaseDetailResponse> Details,
    List<GetPurchasePaymentResponse> Payments);

public sealed record GetPurchaseDetailResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalAmount);

public sealed record GetPurchasePaymentResponse(
    Guid Id,
    int Method,
    string MethodName,
    decimal Amount,
    int Status,
    string StatusName,
    string? Reference,
    string? Notes,
    DateTime Date,
    DateTime CreatedAt);
