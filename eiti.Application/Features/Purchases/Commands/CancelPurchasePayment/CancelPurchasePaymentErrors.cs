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
}
