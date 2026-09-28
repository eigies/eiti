using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Queries.GetSaleInvoicePdf;

public static class GetSaleInvoicePdfErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Sales.InvoicePdf.NotFound",
        "The requested sale was not found.");

    public static readonly Error NotInvoiced = Error.Conflict(
        "Sales.InvoicePdf.NotInvoiced",
        "La venta todavía no tiene un comprobante autorizado.");

    public static Error Unavailable(string? message) => Error.Conflict(
        "Sales.InvoicePdf.Unavailable",
        string.IsNullOrWhiteSpace(message)
            ? "No se pudo obtener el comprobante."
            : message);
}
