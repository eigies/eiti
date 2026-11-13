using System.Globalization;
using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierPaymentOrder;

public static class CancelSupplierPaymentOrderErrors
{
    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

    public static readonly Error NotFound = Error.NotFound(
        "Suppliers.CancelPaymentOrder.NotFound",
        "La orden de pago no existe.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Suppliers.CancelPaymentOrder.AlreadyCancelled",
        "La orden de pago ya está anulada.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Suppliers.CancelPaymentOrder.NoCashSessionOpen",
        "No hay una sesión de caja abierta para reintegrar el efectivo de la orden de pago.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Suppliers.CancelPaymentOrder.NoAssignedCashDrawer",
        "No tenés una caja asignada para reintegrar el efectivo de la orden de pago.");

    // Lo que la orden dejó a favor ya lo consumió otra compra sin back-link a sus pagos (ver CancelSupplierPaymentErrors).
    public static Error CreditAlreadyConsumed(decimal missing) => Error.Conflict(
        "Suppliers.CancelPaymentOrder.CreditAlreadyConsumed",
        $"No se anuló la orden de pago: lo que dejó a favor del proveedor ya se usó en otras compras, y anularla dejaría el saldo a favor en negativo (faltan {Money(missing)}).");

    private static string Money(decimal amount) => amount.ToString("$ #,##0.00", Argentina);
}
