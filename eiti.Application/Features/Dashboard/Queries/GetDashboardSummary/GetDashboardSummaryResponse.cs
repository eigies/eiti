namespace eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;

public sealed record GetDashboardSummaryResponse(
    DashboardPeriodTotals Month,
    DashboardPeriodTotals Today,
    IReadOnlyList<DashboardDayPoint> Days,
    IReadOnlyList<DashboardTopProduct> TopProducts,
    DashboardCollections Collections,
    DashboardTodayStatus TodayStatus,
    IReadOnlyList<DashboardRecentSale> RecentSales,
    DashboardMonthComparison MonthComparison);

// Acumulado del mes contra el mismo tramo del mes anterior.
//
// Las dos series se cortan en el MISMO dia del mes: hoy 16 de agosto, julio tambien llega
// hasta el 16. Comparar un mes completo contra uno a mitad de camino haria parecer siempre
// que se viene peor, que es la forma mas facil de que un grafico mienta.
public sealed record DashboardMonthComparison(
    DateOnly CurrentMonth,
    DateOnly PreviousMonth,
    int DaysElapsed,
    IReadOnlyList<DashboardCumulativePoint> Current,
    IReadOnlyList<DashboardCumulativePoint> Previous);

// Valores ACUMULADOS al cierre de ese dia del mes, no el movimiento del dia.
public sealed record DashboardCumulativePoint(
    int DayOfMonth,
    int Count,
    int Units,
    decimal Amount);

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
