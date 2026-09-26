using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Queries.GetSaleInvoicePrint;

public static class GetSaleInvoicePrintErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Sales.InvoicePrint.NotFound",
        "The requested sale was not found.");

    public static readonly Error NotInvoiced = Error.Conflict(
        "Sales.InvoicePrint.NotInvoiced",
        "La venta todavía no tiene una factura autorizada.");

    public static readonly Error NoCreditNote = Error.Conflict(
        "Sales.InvoicePrint.NoCreditNote",
        "La venta no tiene una nota de crédito autorizada.");

    public static Error Unavailable(string? message) => Error.Conflict(
        "Sales.InvoicePrint.Unavailable",
        string.IsNullOrWhiteSpace(message)
            ? "No se pudieron obtener los datos del comprobante."
            : message);
}
