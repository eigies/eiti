using eiti.Application.Abstractions.Services;
using eiti.Domain.Sales;

namespace eiti.Application.Features.Sales.Common;

/// <summary>
/// A quién se emitieron la factura y su NC, contra lo que dice HOY la ficha del cliente.
///
/// El receptor de un comprobante emitido lo guarda el servicio fiscal, que es el dueño de esos datos: acá
/// se lee de ahí y no se reconstruye desde la ficha. La ficha puede haber cambiado después de facturar
/// (pasó: Baterias Soler, MAT-3705, se cambió el CUIT y la NC salió al CUIT nuevo).
/// </summary>
public sealed record SaleFiscalReceivers(
    bool IsAvailable,
    string? UnavailableReason,
    IReadOnlyList<SaleFiscalDocument> Documents,
    /// <summary>Factura que tuvo CAE (vigente o anulada).</summary>
    SaleFiscalDocument? Invoice,
    FiscalPrintableDocument? InvoicePrinted,
    FiscalReceiver? CustomerReceiver,
    /// <summary>Última NC autorizada de la venta, si hay.</summary>
    SaleFiscalDocument? CreditNote,
    FiscalPrintableDocument? CreditNotePrinted,
    /// <summary>Última ND autorizada (regularización), si hay.</summary>
    SaleFiscalDocument? DebitNote = null,
    FiscalPrintableDocument? DebitNotePrinted = null)
{
    public bool InvoiceIsLive => Invoice?.Status == SaleInvoicingStatus.Invoiced;

    /// <summary>Sin factura no hay nada que comparar: coincide.</summary>
    public bool CustomerMatchesInvoice =>
        InvoicePrinted is null || FiscalReceiverIdentity.Same(CustomerReceiver, InvoicePrinted.Receiver);

    public bool CreditNoteMatchesInvoice =>
        CreditNotePrinted is null || InvoicePrinted is null ||
        FiscalReceiverIdentity.Same(CreditNotePrinted.Receiver, InvoicePrinted.Receiver);

    /// <summary>
    /// Qué falta para regularizar una NC que salió a otro receptor que el de su factura: primero la ND que
    /// la neutraliza, después la NC al receptor de la factura. Se recalcula de los comprobantes, así que
    /// retomar después de un corte (ND en trámite, NC rechazada) sigue desde donde quedó.
    /// </summary>
    public SaleCreditNoteRegularizationStep RegularizationStep
    {
        get
        {
            var neutralized = Documents
                .Where(document => document.Kind == SaleFiscalDocumentKind.CreditNote && document.Status == SaleInvoicingStatus.Voided)
                .MaxBy(document => document.Sequence);

            if (CreditNote is not null && (neutralized is null || CreditNote.Sequence > neutralized.Sequence))
            {
                return CreditNoteMatchesInvoice
                    ? SaleCreditNoteRegularizationStep.None
                    : SaleCreditNoteRegularizationStep.DebitNote;
            }

            return neutralized is not null && Invoice?.Status == SaleInvoicingStatus.Voided
                ? SaleCreditNoteRegularizationStep.CreditNote
                : SaleCreditNoteRegularizationStep.None;
        }
    }

    public static SaleFiscalReceivers Unavailable(IReadOnlyList<SaleFiscalDocument> documents, string? reason) =>
        new(false, reason, documents, null, null, null, null, null);
}

public enum SaleCreditNoteRegularizationStep
{
    None = 0,
    /// <summary>Emitir la ND que neutraliza la NC mal emitida.</summary>
    DebitNote = 1,
    /// <summary>La NC mal emitida ya está neutralizada: emitir la NC al receptor de la factura.</summary>
    CreditNote = 2
}

/// <summary>
/// Lo que identifica fiscalmente a un receptor: condición de IVA (define la letra) y documento, por dígitos.
/// El nombre no: la razón social cargada puede variar. Es la misma regla que aplica el servicio fiscal.
/// </summary>
public static class FiscalReceiverIdentity
{
    public static bool Same(FiscalReceiver? left, FiscalReceiver? right) => Of(left) == Of(right);

    private static (FiscalReceiverVatCondition VatCondition, string? DocType, string? DocNumber) Of(FiscalReceiver? receiver)
    {
        var number = SaleInvoicingReceiverRules.OnlyDigits(receiver?.DocNumber);
        return receiver is null || number is null
            ? (receiver?.VatCondition ?? FiscalReceiverVatCondition.FinalConsumer, null, null)
            : (receiver.VatCondition, receiver.DocType?.Trim().ToUpperInvariant(), number);
    }
}
