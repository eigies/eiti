using eiti.Application.Common;

namespace eiti.Application.Features.Purchases.Commands.AddPurchasePayment;

public static class AddPurchasePaymentErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Purchases.AddPayment.NotFound",
        "The purchase was not found.");

    public static readonly Error PurchaseAlreadyPaid = Error.Conflict(
        "Purchases.AddPayment.AlreadyPaid",
        "The purchase is already fully paid.");

    public static readonly Error PurchaseCancelled = Error.Conflict(
        "Purchases.AddPayment.Cancelled",
        "Cannot add payments to a cancelled purchase.");

    public static readonly Error InvalidAmount = Error.Validation(
        "Purchases.AddPayment.InvalidAmount",
        "Payment amount must be greater than zero.");

    public static readonly Error InvalidPaymentMethod = Error.Validation(
        "Purchases.AddPayment.InvalidPaymentMethod",
        "The specified payment method is invalid.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Purchases.AddPayment.NoCashSessionOpen",
        "A cash payment requires an open cash session.");
}
