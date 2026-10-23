using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierPaymentOrder;

public static class CancelSupplierPaymentOrderErrors
{
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
}
