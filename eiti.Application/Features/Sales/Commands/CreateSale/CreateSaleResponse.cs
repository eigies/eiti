namespace eiti.Application.Features.Sales.Commands.CreateSale;

public sealed record CreateSaleResponse(
    Guid Id,
    Guid BranchId,
    Guid? CustomerId,
    string? CustomerFullName,
    string? CustomerDocument,
    string? CustomerTaxId,
    string? CustomerAddress,
    Guid? CashSessionId,
    bool HasDelivery,
    Guid? TransportAssignmentId,
    int IdSaleStatus,
    string SaleStatus,
    decimal NoDeliverySurchargeTotal,
    decimal TotalAmount,
    decimal MonetaryPaidAmount,
    decimal TradeInAmount,
    decimal SettledAmount,
    decimal PendingAmount,
    decimal ChangeAmount,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? UpdatedAt,
    bool IsModified,
    IReadOnlyList<CreateSaleDetailItemResponse> Details,
    IReadOnlyList<CreateSalePaymentItemResponse> Payments,
    IReadOnlyList<CreateSaleTradeInItemResponse> TradeIns);

public sealed record CreateSaleDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount);

public sealed record CreateSalePaymentItemResponse(
    int IdPaymentMethod,
    string PaymentMethod,
    decimal Amount,
    string? Reference);

public sealed record CreateSaleTradeInItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal Amount);
