using eiti.Application.Common;

namespace eiti.Application.Features.Purchases.Commands.CancelPurchasePayment;

public static class CancelPurchasePaymentErrors
{
    public static readonly Error PurchaseNotFound = Error.NotFound(
        "Purchases.CancelPayment.PurchaseNotFound",
        "The purchase was not found.");

    public static readonly Error PurchaseCancelled = Error.Conflict(
        "Purchases.CancelPayment.PurchaseCancelled",
        "Cannot modify payments on a cancelled purchase.");

    public static readonly Error PaymentNotFound = Error.NotFound(
        "Purchases.CancelPayment.PaymentNotFound",
        "The payment was not found.");

    public static readonly Error PaymentAlreadyCancelled = Error.Conflict(
        "Purchases.CancelPayment.PaymentAlreadyCancelled",
        "The payment is already cancelled.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Purchases.CancelPayment.NoCashSessionOpen",
        "No hay una sesión de caja abierta para reintegrar el efectivo del pago anulado. Abrí la caja antes de anular el pago.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Purchases.CancelPayment.NoAssignedCashDrawer",
        "No tenés una caja asignada. Pedile a un administrador que te asigne una caja para poder anular pagos en efectivo.");
}
