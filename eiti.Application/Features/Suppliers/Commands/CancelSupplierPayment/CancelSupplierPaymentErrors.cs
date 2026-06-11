using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierPayment;

public static class CancelSupplierPaymentErrors
{
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
}
