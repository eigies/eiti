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
        "No hay una sesión de caja abierta para tu caja asignada.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Purchases.Create.NoAssignedCashDrawer",
        "No tenés una caja asignada. Pedile a un administrador que te asigne una caja para poder registrar pagos.");

    public static Error ProductNotFound(Guid productId) => Error.NotFound(
        "Purchases.Create.ProductNotFound",
        $"The product '{productId}' was not found.");

    public static readonly Error InvalidPaymentMethod = Error.Validation(
        "Purchases.Create.InvalidPaymentMethod",
        "The specified payment method is invalid.");

    public static readonly Error DuplicateInvoiceNumber = Error.Conflict(
        "Purchases.Create.DuplicateInvoiceNumber",
        "Ya existe una compra con ese número de factura para el mismo proveedor.");
}
