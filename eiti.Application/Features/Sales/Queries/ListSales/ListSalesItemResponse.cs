using eiti.Domain.Sales;

namespace eiti.Application.Features.Sales.Queries.ListSales;

public sealed record ListSalesItemResponse(
    Guid Id,
    Guid BranchId,
    Guid? CustomerId,
    string? CustomerFullName,
    string? CustomerDocument,
    string? CustomerTaxId,
    Guid? CashSessionId,
    bool HasDelivery,
    Guid? TransportAssignmentId,
    string? DriverFullName,
    string? VehiclePlate,
    int? TransportStatus,
    string? TransportStatusName,
    int IdSaleStatus,
    string SaleStatus,
    decimal NoDeliverySurchargeTotal,
    decimal TotalAmount,
    decimal MonetaryPaidAmount,
    decimal TradeInAmount,
    decimal SettledAmount,
    decimal PendingAmount,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? UpdatedAt,
    bool IsModified,
    SaleSourceChannel? SourceChannel,
    IReadOnlyList<ListSalesDetailItemResponse> Details,
    IReadOnlyList<ListSalesPaymentItemResponse> Payments,
    IReadOnlyList<ListSalesTradeInItemResponse> TradeIns);

public sealed record ListSalesDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount);

public sealed record ListSalesPaymentItemResponse(
    int IdPaymentMethod,
    string PaymentMethod,
    decimal Amount,
    string? Reference);

public sealed record ListSalesTradeInItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal Amount);
