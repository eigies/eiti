using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.TradeInsByBranch;

// Reporte de productos recibidos en canje (trade-in), agrupados por sucursal y producto.
// La fecha del canje es la de la venta que lo recibió: el ingreso de stock (TradeInIn) se
// registra en el mismo momento en que se crea la venta.
public sealed record TradeInsByBranchQuery(
    DateTime DateFrom,
    DateTime DateTo,
    Guid? BranchId = null,
    Guid? CustomerId = null
) : IRequest<Result<TradeInsByBranchResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ReportsSalesTradeIns];
}
