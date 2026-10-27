using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.RegularizeSaleCreditNote;

public static class RegularizeSaleCreditNoteErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Sales.CreditNoteRegularization.NotFound",
        "The requested sale was not found.");

    public static readonly Error NotConfigured = Error.Conflict(
        "Sales.CreditNoteRegularization.NotConfigured",
        "La facturación electrónica no está configurada para esta instalación.");

    public static readonly Error NothingToRegularize = Error.Conflict(
        "Sales.CreditNoteRegularization.NothingToRegularize",
        "La nota de crédito de la venta salió al mismo receptor que la factura: no hay nada que regularizar.");

    public static readonly Error ConfirmationRequired = Error.Validation(
        "Sales.CreditNoteRegularization.ConfirmationRequired",
        "Regularizar emite una nota de débito y una nota de crédito: confirmalo para continuar.");

    public static Error Unavailable(string? message) => Error.Conflict(
        "Sales.CreditNoteRegularization.Unavailable",
        string.IsNullOrWhiteSpace(message)
            ? "No se pudo consultar al servicio de facturación a quién se emitieron los comprobantes."
            : message);

    public static Error Rejected(string? message) => Error.Conflict(
        "Sales.CreditNoteRegularization.Rejected",
        string.IsNullOrWhiteSpace(message)
            ? "El servicio de facturación rechazó el comprobante."
            : message);
}
