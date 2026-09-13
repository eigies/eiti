using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.InvoiceSale;

public static class InvoiceSaleErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Sales.Invoice.NotFound",
        "The requested sale was not found.");

    public static readonly Error NotConfigured = Error.Conflict(
        "Sales.Invoice.NotConfigured",
        "La facturación electrónica no está configurada para esta instalación.");

    public static readonly Error SaleCancelled = Error.Conflict(
        "Sales.Invoice.SaleCancelled",
        "No se puede facturar una venta anulada.");

    public static readonly Error AlreadyInvoiced = Error.Conflict(
        "Sales.Invoice.AlreadyInvoiced",
        "La venta ya tiene un comprobante autorizado.");

    public static Error Rejected(string? message) => Error.Conflict(
        "Sales.Invoice.Rejected",
        string.IsNullOrWhiteSpace(message)
            ? "El servicio de facturación rechazó el comprobante."
            : message);
}
