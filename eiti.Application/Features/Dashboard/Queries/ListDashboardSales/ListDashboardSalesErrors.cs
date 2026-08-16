using eiti.Application.Common;

namespace eiti.Application.Features.Dashboard.Queries.ListDashboardSales;

public static class ListDashboardSalesErrors
{
    public static readonly Error SingleDayRequired = Error.Validation(
        "Dashboard.Sales.SingleDayRequired",
        "El detalle del dashboard admite un solo dia por consulta.");

    public static readonly Error DateOutsideWindow = Error.Validation(
        "Dashboard.Sales.DateOutsideWindow",
        "El detalle del dashboard solo esta disponible para los ultimos siete dias.");
}
