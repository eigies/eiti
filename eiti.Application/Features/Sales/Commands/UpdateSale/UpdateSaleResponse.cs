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
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? UpdatedAt,
    bool IsModified,
    IReadOnlyList<UpdateSaleDetailItemResponse> Details);

public sealed record UpdateSaleDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount);
