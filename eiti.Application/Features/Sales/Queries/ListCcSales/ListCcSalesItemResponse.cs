namespace eiti.Application.Features.Sales.Queries.ListCcSales;

public sealed record ListCcSalesItemResponse(
    Guid Id, string? Code, string? CustomerFullName,
    DateTime CreatedAt, decimal TotalAmount, decimal CcPaidTotal,
    decimal CcPendingAmount, int IdSaleStatus, string SaleStatus,
    bool IsCuentaCorriente);
