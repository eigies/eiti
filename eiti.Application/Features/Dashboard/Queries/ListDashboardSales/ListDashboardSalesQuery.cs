using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Dashboard.Queries.ListDashboardSales;

// No exige sales.access: el detalle es parte del dashboard y replica sus controles de
// autenticacion, sucursal e importes financieros dentro del handler.
public sealed record ListDashboardSalesQuery(
    DateTime DateFrom,
    DateTime DateTo,
    Guid? BranchId = null
) : IRequest<Result<IReadOnlyList<DashboardSaleResponse>>>;
