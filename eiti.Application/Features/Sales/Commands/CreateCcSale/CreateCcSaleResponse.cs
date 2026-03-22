namespace eiti.Application.Features.Sales.Commands.CreateCcSale;

public sealed record CreateCcSaleResponse(
    Guid Id,
    string? Code,
    Guid BranchId,
    Guid CustomerId,
    string? CustomerFullName,
    int IdSaleStatus,
    string SaleStatus,
    decimal TotalAmount,
    bool IsCuentaCorriente,
    DateTime CreatedAt,
    IReadOnlyList<CreateCcSaleDetailItemResponse> Details);

public sealed record CreateCcSaleDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount);
