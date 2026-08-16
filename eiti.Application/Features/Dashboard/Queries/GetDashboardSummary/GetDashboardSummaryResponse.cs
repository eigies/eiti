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

// Count  = operaciones. Con filtro de categoria, ventas que incluyen al menos una unidad
//          de esas categorias (una venta con bateria + accesorio cuenta una vez).
// Units  = unidades vendidas. Es lo que compara contra el reporte de ventas, que tambien
//          cuenta unidades. Sin filtro son todas las lineas; con filtro, solo las de esas
//          categorias.
// Amount = facturacion, NO lo cobrado (eso vive en Collections). Sin filtro es el total de
//          la venta e incluye descuentos y recargos; con filtro es la suma de las lineas
//          de las categorias elegidas.
public sealed record DashboardSegment(int Count, int Units, decimal Amount);

public sealed record DashboardDayPoint(
    DateOnly Date,
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
