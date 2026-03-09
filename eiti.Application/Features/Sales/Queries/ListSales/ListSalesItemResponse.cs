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
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? UpdatedAt,
    bool IsModified,
    IReadOnlyList<ListSalesDetailItemResponse> Details);

public sealed record ListSalesDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount);
