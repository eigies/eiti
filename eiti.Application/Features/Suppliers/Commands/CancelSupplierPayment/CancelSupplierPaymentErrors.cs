using System.Globalization;
using eiti.Application.Common;
using eiti.Application.Features.Suppliers.Common;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierPayment;

public static class CancelSupplierPaymentErrors
{
    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

    public static readonly Error PaymentNotFound = Error.NotFound(
        "Suppliers.CancelPayment.NotFound",
        "El pago no existe.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Suppliers.CancelPayment.AlreadyCancelled",
        "El pago ya está anulado.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Suppliers.CancelPayment.NoCashSessionOpen",
        "No hay una sesión de caja abierta para reintegrar el efectivo del pago.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Suppliers.CancelPayment.NoAssignedCashDrawer",
        "No tenés una caja asignada para reintegrar el efectivo del pago.");

    public static readonly Error PartOfOrder = SupplierPaymentLineErrors.PartOfOrder;

    // Lo que el pago dejó a favor ya lo consumió otra compra sin back-link al pago: revertirlo saca su importe entero
    // del saldo y solo recupera lo imputado con su back-link. Mismo criterio que anular una NC (CreditAlreadyConsumed).
    public static Error CreditAlreadyConsumed(decimal missing) => Error.Conflict(
        "Suppliers.CancelPayment.CreditAlreadyConsumed",
        $"No se anuló el pago: lo que dejó a favor del proveedor ya se usó en otras compras, y anularlo dejaría el saldo a favor en negativo (faltan {Money(missing)}).");

    private static string Money(decimal amount) => amount.ToString("$ #,##0.00", Argentina);
}
