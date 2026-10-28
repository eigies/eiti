using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.GetSaleFiscalReceivers;

/// <summary>
/// A quién se emitieron la factura y su nota de crédito (según el servicio fiscal) contra la ficha actual
/// del cliente. Lo usa el front para mostrar el CUIT facturado y, antes de anular o regularizar, los dos
/// receptores cuando difieren.
/// </summary>
public sealed record GetSaleFiscalReceiversQuery(Guid SaleId)
    : IRequest<Result<GetSaleFiscalReceiversResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesAccess];
}
