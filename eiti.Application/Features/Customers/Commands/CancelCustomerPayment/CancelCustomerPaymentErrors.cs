using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerPayment;

public static class CancelCustomerPaymentErrors
{
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
}
