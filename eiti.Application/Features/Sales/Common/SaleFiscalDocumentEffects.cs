using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Sales;

namespace eiti.Application.Features.Sales.Common;

/// <summary>
/// Efectos que una nota de crédito autorizada produce sobre la factura que anula.
///
/// Vive acá y no en los handlers porque la NC se puede autorizar por DOS caminos —el intento
/// sincrónico y el callback diferido— y la transición tiene que ser exactamente la misma en los
/// dos. Un solo lugar, un solo comportamiento.
/// </summary>
public static class SaleFiscalDocumentEffects
{
    /// <summary>
    /// Si el comprobante es una NC ya autorizada, anula la factura referenciada.
    /// Idempotente: repetirlo no cambia nada (el callback es at-least-once).
    /// </summary>
    public static async Task ApplyAsync(
        SaleFiscalDocument document,
        ISaleFiscalDocumentRepository fiscalDocuments,
        CancellationToken cancellationToken)
    {
        if (document.Kind != SaleFiscalDocumentKind.CreditNote ||
            !document.IsAuthorized ||
            document.ReversedDocumentId is null)
        {
            return;
        }

        var invoice = await fiscalDocuments.GetByIdAsync(document.ReversedDocumentId, cancellationToken);

        // Solo se anula una factura que sigue vigente: si ya está Voided, Void() no hace nada.
        if (invoice is not null && invoice.Status is SaleInvoicingStatus.Invoiced or SaleInvoicingStatus.Voided)
        {
            invoice.Void();
        }
    }
}
