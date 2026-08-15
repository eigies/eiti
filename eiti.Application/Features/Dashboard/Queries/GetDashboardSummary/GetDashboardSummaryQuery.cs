using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;

// Agregados del dashboard inicial. DateFrom/DateTo llegan como fecha local del usuario
// (el mes en curso) y se traducen a UTC con BusinessCalendar en el handler.
// BranchId null = todas las sucursales que el usuario tenga permitidas.
//
// No implementa IRequirePermissions a proposito: la ruta /dashboard hoy solo exige estar
// autenticado, y hay perfiles ("Cajero") sin sales.access que deben poder entrar igual.
// Lo sensible son los importes, y eso lo gatea DashboardViewFinancials dentro del handler.
public sealed record GetDashboardSummaryQuery(
    DateTime DateFrom,
    DateTime DateTo,
    Guid? BranchId = null
) : IRequest<Result<GetDashboardSummaryResponse>>;
