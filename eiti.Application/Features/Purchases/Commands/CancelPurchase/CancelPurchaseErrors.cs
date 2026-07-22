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

    public static readonly Error SupplierNotFound = Error.NotFound(
        "Purchases.Cancel.SupplierNotFound",
        "The supplier for this purchase was not found.");

    public static readonly Error RefundModeRequired = Error.Validation(
        "Purchases.Cancel.RefundModeRequired",
        "La compra tiene pagos imputados. Elegí qué hacer con lo pagado: saldo a favor o revertir el pago.");

    public static readonly Error NoAssignedCashDrawer = Error.Validation(
        "Purchases.Cancel.NoAssignedCashDrawer",
        "Para revertir un pago en efectivo necesitás una caja asignada (o permiso para ver todas las cajas).");

    public static readonly Error NoCashSessionOpen = Error.Validation(
        "Purchases.Cancel.NoCashSessionOpen",
        "Para revertir un pago en efectivo necesitás una caja abierta.");

    public static readonly Error SupplierPaymentNotFound = Error.NotFound(
        "Purchases.Cancel.SupplierPaymentNotFound",
        "No se encontró el pago de proveedor asociado a la compra.");
}
