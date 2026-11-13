using System.Globalization;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Common;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerPayment;

public static class CancelCustomerPaymentErrors
{
    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

    public static readonly Error PaymentNotFound = Error.NotFound(
        "Customers.CancelPayment.NotFound",
        "El cobro no existe.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Customers.CancelPayment.AlreadyCancelled",
        "El cobro ya está anulado.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Customers.CancelPayment.NoCashSessionOpen",
        "No hay una sesión de caja abierta para reintegrar el efectivo del cobro.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Customers.CancelPayment.NoAssignedCashDrawer",
        "No tenés una caja asignada para reintegrar el efectivo del cobro.");

    public static readonly Error PartOfReceipt = CustomerPaymentLineErrors.PartOfReceipt;

    // Lo que el cobro dejó a favor ya lo consumió otra venta sin back-link al cobro: revertirlo saca su importe entero
    // del saldo y solo recupera lo imputado con su back-link. Mismo criterio que anular una NC (CreditAlreadyConsumed).
    public static Error CreditAlreadyConsumed(decimal missing) => Error.Conflict(
        "Customers.CancelPayment.CreditAlreadyConsumed",
        $"No se anuló el cobro: lo que dejó a favor del cliente ya se usó en otras ventas, y anularlo dejaría el saldo a favor en negativo (faltan {Money(missing)}).");

    private static string Money(decimal amount) => amount.ToString("$ #,##0.00", Argentina);
}
