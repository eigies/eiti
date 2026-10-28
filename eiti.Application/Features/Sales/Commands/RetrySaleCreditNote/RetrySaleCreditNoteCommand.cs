using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.RetrySaleCreditNote;

/// <summary>
/// Vuelve a pedir la nota de crédito de una venta anulada cuya factura sigue vigente: la NC quedó rechazada,
/// en trámite, o no salió porque el servicio fiscal no respondió al anular.
/// </summary>
/// <param name="ConfirmInvoiceReceiver">
/// El usuario vio que la factura se emitió a otro receptor que el de la ficha actual y confirmó que la NC
/// salga al de la factura.
/// </param>
public sealed record RetrySaleCreditNoteCommand(Guid Id, bool ConfirmInvoiceReceiver = false)
    : IRequest<Result<RetrySaleCreditNoteResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesInvoice];
}
