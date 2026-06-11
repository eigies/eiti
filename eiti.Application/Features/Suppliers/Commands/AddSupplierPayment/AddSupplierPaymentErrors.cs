using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;

public static class AddSupplierPaymentErrors
{
    public static readonly Error SupplierNotFound = Error.NotFound(
        "Suppliers.AddPayment.SupplierNotFound",
        "El proveedor no existe.");

    public static readonly Error InvalidPaymentMethod = Error.Validation(
        "Suppliers.AddPayment.InvalidPaymentMethod",
        "El método de pago es inválido.");

    public static readonly Error InvalidAmount = Error.Validation(
        "Suppliers.AddPayment.InvalidAmount",
        "El monto debe ser mayor a cero.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Suppliers.AddPayment.NoCashSessionOpen",
        "No hay una sesión de caja abierta para tu caja asignada.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Suppliers.AddPayment.NoAssignedCashDrawer",
        "No tenés una caja asignada. Pedile a un administrador que te asigne una caja para poder registrar pagos.");

    public static readonly Error CashSessionFromPreviousDay = Error.Conflict(
        "Suppliers.AddPayment.CashSessionFromPreviousDay",
        "La sesión de caja está abierta desde un día anterior. Cerrá la sesión antes de registrar pagos.");

    public static readonly Error ChequeRequired = Error.Validation(
        "Suppliers.AddPayment.ChequeRequired",
        "Para pagar con cheque debe seleccionar un cheque en cartera.");

    public static readonly Error ChequeNotFound = Error.NotFound(
        "Suppliers.AddPayment.ChequeNotFound",
        "El cheque seleccionado no existe.");

    public static readonly Error ChequeNotEnCartera = Error.Conflict(
        "Suppliers.AddPayment.ChequeNotEnCartera",
        "El cheque seleccionado no está en cartera y no puede usarse para pagar.");
}
