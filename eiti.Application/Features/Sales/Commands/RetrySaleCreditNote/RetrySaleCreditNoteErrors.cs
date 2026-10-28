using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.RetrySaleCreditNote;

public static class RetrySaleCreditNoteErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Sales.CreditNote.NotFound",
        "The requested sale was not found.");

    public static readonly Error NotConfigured = Error.Conflict(
        "Sales.CreditNote.NotConfigured",
        "La facturación electrónica no está configurada para esta instalación.");

    public static readonly Error SaleNotCancelled = Error.Conflict(
        "Sales.CreditNote.SaleNotCancelled",
        "La nota de crédito se emite al anular la venta.");

    public static readonly Error NothingToReverse = Error.Conflict(
        "Sales.CreditNote.NothingToReverse",
        "La venta no tiene una factura vigente que anular.");

    // Mismo código de reacción que al anular: el front muestra los dos receptores y reenvía confirmando.
    public static readonly Error InvoiceReceiverChanged = Error.Validation(
        "Sales.CreditNote.InvoiceReceiverChanged",
        "La factura se emitió a otro receptor que el de la ficha actual del cliente. La nota de crédito tiene que salir al receptor de la factura: confirmalo para emitirla.");

    public static Error Unavailable(string? message) => Error.Conflict(
        "Sales.CreditNote.Unavailable",
        string.IsNullOrWhiteSpace(message)
            ? "No se pudo consultar al servicio de facturación a quién se emitió la factura."
            : message);

    public static Error Rejected(string? message) => Error.Conflict(
        "Sales.CreditNote.Rejected",
        string.IsNullOrWhiteSpace(message)
            ? "El servicio de facturación rechazó la nota de crédito."
            : message);
}
