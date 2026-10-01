using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Commands.DeleteFiscalPointOfSale;

/// <summary>Borra un punto de venta cargado por error. Si una sucursal lo usa, primero hay que desasignarlo.</summary>
public sealed record DeleteFiscalPointOfSaleCommand(Guid PointOfSaleId) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesInvoice];
}
