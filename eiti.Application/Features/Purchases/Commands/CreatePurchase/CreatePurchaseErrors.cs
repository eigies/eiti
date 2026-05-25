using eiti.Application.Common;

namespace eiti.Application.Features.Purchases.Commands.CreatePurchase;

public static class CreatePurchaseErrors
{
    public static readonly Error SupplierNotFound = Error.NotFound(
        "Purchases.Create.SupplierNotFound",
        "The specified supplier was not found.");

    public static readonly Error InsufficientDetails = Error.Validation(
        "Purchases.Create.InsufficientDetails",
        "A purchase must contain at least one detail.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Purchases.Create.NoCashSessionOpen",
        "A cash payment requires an open cash session.");

    public static Error ProductNotFound(Guid productId) => Error.NotFound(
        "Purchases.Create.ProductNotFound",
        $"The product '{productId}' was not found.");

    public static readonly Error InvalidPaymentMethod = Error.Validation(
        "Purchases.Create.InvalidPaymentMethod",
        "The specified payment method is invalid.");
}
