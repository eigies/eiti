using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Sales;

namespace eiti.Application.Features.Sales.Common;

/// <summary>
/// Efectos que un comprobante autorizado produce sobre su asociado: una NC anula la factura; una ND
/// neutraliza la NC.
///
/// Vive acá y no en los handlers porque la NC se puede autorizar por DOS caminos —el intento
/// sincrónico y el callback diferido— y la transición tiene que ser exactamente la misma en los
/// dos. Un solo lugar, un solo comportamiento.
/// </summary>
public static class SaleFiscalDocumentEffects
{
    /// <summary>
    /// Si el comprobante es una NC o ND ya autorizada, deja sin efecto su asociado.
    /// Idempotente: repetirlo no cambia nada (el callback es at-least-once).
    /// </summary>
    public static async Task ApplyAsync(
        SaleFiscalDocument document,
        ISaleFiscalDocumentRepository fiscalDocuments,
        CancellationToken cancellationToken)
    {
        if (document.Kind is not (SaleFiscalDocumentKind.CreditNote or SaleFiscalDocumentKind.DebitNote) ||
            !document.IsAuthorized ||
            document.ReversedDocumentId is null)
        {
            return;
        }

        var associated = await fiscalDocuments.GetByIdAsync(document.ReversedDocumentId, cancellationToken);

        // Solo se anula un asociado autorizado: si ya está Voided, Void() no hace nada.
        if (associated is not null && associated.Status is SaleInvoicingStatus.Invoiced or SaleInvoicingStatus.Voided)
        {
            associated.Void();
        }
    }
}
