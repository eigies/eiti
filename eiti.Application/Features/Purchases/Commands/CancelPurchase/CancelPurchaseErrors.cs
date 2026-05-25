using eiti.Application.Common;

namespace eiti.Application.Features.Purchases.Commands.CancelPurchase;

public static class CancelPurchaseErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Purchases.Cancel.NotFound",
        "The purchase was not found.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Purchases.Cancel.AlreadyCancelled",
        "The purchase is already cancelled.");

    public static readonly Error CannotCancelPaid = Error.Conflict(
        "Purchases.Cancel.CannotCancelPaid",
        "A fully paid purchase cannot be cancelled. Cancel payments first.");
}
