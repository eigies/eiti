namespace eiti.Application.Features.Dashboard.Queries.ListDashboardSales;

public sealed record DashboardSaleResponse(
    Guid Id,
    string? Code,
    DateTime CreatedAt,
    string CustomerName,
    int SaleStatus,
    decimal TotalAmount,
    bool IsCuentaCorriente);
