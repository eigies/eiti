namespace eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;

public sealed record GetDashboardSummaryResponse(
    DashboardPeriodTotals Month,
    DashboardPeriodTotals Today,
    IReadOnlyList<DashboardDayPoint> Days,
    IReadOnlyList<DashboardTopProduct> TopProducts,
    DashboardCollections Collections,
    DashboardTodayStatus TodayStatus,
    IReadOnlyList<DashboardRecentSale> RecentSales);

// Minorista = venta normal; CuentaCorriente = IsCuentaCorriente. Misma definicion que
// SalesReport y WholesaleByCustomer. Las canceladas quedan afuera de los tres segmentos.
public sealed record DashboardPeriodTotals(
    DashboardSegment Total,
    DashboardSegment Retail,
    DashboardSegment CurrentAccount);

// Amount = facturacion de las ventas activas, NO lo cobrado. Lo cobrado vive en Collections.
public sealed record DashboardSegment(int Count, decimal Amount);

public sealed record DashboardDayPoint(
    DateTime Date,
    int RetailCount,
    decimal RetailAmount,
    int CurrentAccountCount,
    decimal CurrentAccountAmount);

public sealed record DashboardTopProduct(
    Guid ProductId,
    string Name,
    string Brand,
    int Units,
    int SalesCount);

public sealed record DashboardCollections(
    decimal PaidAmount,
    int PaidCount,
    decimal PendingAmount,
    int PendingCount,
    decimal AvgTicket);

public sealed record DashboardTodayStatus(
    int ActiveCount,
    int PaidCount,
    int PendingCount,
    int CancelledCount);

public sealed record DashboardRecentSale(
    Guid Id,
    string? Code,
    DateTime CreatedAt,
    string CustomerName,
    int SaleStatus,
    decimal TotalAmount,
    bool IsCuentaCorriente);
