using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.RegularizeSaleCreditNote;

/// <summary>
/// Regulariza una nota de crédito que salió a otro receptor que el de la factura que anulaba: emite una nota
/// de débito a ese receptor (la neutraliza) y una nota de crédito nueva al receptor de la factura. Si se corta
/// a mitad de camino, repetirlo retoma desde donde quedó.
/// </summary>
/// <param name="Confirm">
/// El usuario vio los receptores y confirmó: son comprobantes fiscales que no se deshacen.
/// </param>
public sealed record RegularizeSaleCreditNoteCommand(Guid Id, bool Confirm = false)
    : IRequest<Result<RegularizeSaleCreditNoteResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesInvoiceRegularize];
}
