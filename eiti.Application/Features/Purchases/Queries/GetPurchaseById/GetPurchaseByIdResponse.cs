using eiti.Domain.Purchases;

namespace eiti.Application.Features.Purchases.Queries.GetPurchaseById;

public sealed record GetPurchaseByIdResponse(
    Guid Id,
    string Code,
    Guid BranchId,
    Guid? SupplierId,
    string? SupplierName,
    string? SupplierPhone,
    string? SupplierEmail,
    string? InvoiceNumber,
    string? Notes,
    decimal? IvaPct,
    decimal? IngresosBrutosPct,
    int Status,
    string StatusName,
    decimal TotalAmount,
    decimal TaxAmount,
    decimal GrandTotal,
    decimal TotalPaid,
    decimal PendingAmount,
    DateTime CreatedAt,
    Guid CreatedByUserId,
    DateTime? PaidAt,
    DateTime? CancelledAt,
    List<GetPurchaseDetailResponse> Details,
    List<GetPurchasePaymentResponse> Payments,
    decimal SupplierCreditBalance);

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
    DateTime CreatedAt,
    Guid? ChequeId = null,
    string? ChequeNumero = null);
