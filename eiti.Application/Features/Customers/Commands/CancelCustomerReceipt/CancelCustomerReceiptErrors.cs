using System.Globalization;
using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerReceipt;

public static class CancelCustomerReceiptErrors
{
    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

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

    // Lo que el recibo dejó a favor ya lo consumió otra venta sin back-link a sus cobros (ver CancelCustomerPaymentErrors).
    public static Error CreditAlreadyConsumed(decimal missing) => Error.Conflict(
        "Customers.CancelReceipt.CreditAlreadyConsumed",
        $"No se anuló el recibo: lo que dejó a favor del cliente ya se usó en otras ventas, y anularlo dejaría el saldo a favor en negativo (faltan {Money(missing)}).");

    private static string Money(decimal amount) => amount.ToString("$ #,##0.00", Argentina);
}
