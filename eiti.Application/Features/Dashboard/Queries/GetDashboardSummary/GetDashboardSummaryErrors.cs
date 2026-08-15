using eiti.Application.Common;

namespace eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;

internal static class GetDashboardSummaryErrors
{
    public static readonly Error BranchNotAllowed = Error.Forbidden(
        "Dashboard.Summary.BranchNotAllowed",
        "No tenes acceso a la sucursal solicitada.");
}
