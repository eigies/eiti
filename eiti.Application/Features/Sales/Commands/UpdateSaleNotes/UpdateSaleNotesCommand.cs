using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.UpdateSaleNotes;

/// <summary>
/// Corrige las observaciones de una venta ya creada. Va por separado de UpdateSale a propósito: aquel solo
/// acepta ventas en espera y rearma detalle, pagos y costos; la nota se corrige en cualquier estado salvo anulada.
/// </summary>
public sealed record UpdateSaleNotesCommand(Guid SaleId, string? Notes) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesUpdate];
}
