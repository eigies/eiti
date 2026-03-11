namespace eiti.Application.Features.Sales.Commands.UpdateSale;

public sealed record UpdateSaleResponse(
    Guid Id,
    Guid BranchId,
    Guid? CustomerId,
    string? CustomerFullName,
    string? CustomerDocument,
    string? CustomerTaxId,
    Guid? CashSessionId,
    bool HasDelivery,
    Guid? TransportAssignmentId,
    int IdSaleStatus,
    string SaleStatus,
    decimal TotalAmount,
    decimal MonetaryPaidAmount,
    decimal TradeInAmount,
    decimal SettledAmount,
    decimal PendingAmount,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? UpdatedAt,
    bool IsModified,
    IReadOnlyList<UpdateSaleDetailItemResponse> Details,
    IReadOnlyList<UpdateSalePaymentItemResponse> Payments,
    IReadOnlyList<UpdateSaleTradeInItemResponse> TradeIns);

public sealed record UpdateSaleDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount);

public sealed record UpdateSalePaymentItemResponse(
    int IdPaymentMethod,
    string PaymentMethod,
    decimal Amount,
    string? Reference);

public sealed record UpdateSaleTradeInItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal Amount);
