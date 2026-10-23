using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerReceipt;

public static class CancelCustomerReceiptErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Customers.CancelReceipt.NotFound",
        "El recibo no existe.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Customers.CancelReceipt.AlreadyCancelled",
        "El recibo ya está anulado.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Customers.CancelReceipt.NoCashSessionOpen",
        "No hay una sesión de caja abierta para reintegrar el efectivo del recibo.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Customers.CancelReceipt.NoAssignedCashDrawer",
        "No tenés una caja asignada para reintegrar el efectivo del recibo.");
}
