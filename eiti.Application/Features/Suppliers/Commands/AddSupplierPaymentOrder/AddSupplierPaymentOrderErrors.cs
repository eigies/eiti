using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPaymentOrder;

// Los errores de cada medio (cheque, monto, método) están en SupplierPaymentLineErrors.
public static class AddSupplierPaymentOrderErrors
{
    public static readonly Error SupplierNotFound = Error.NotFound(
        "Suppliers.AddPaymentOrder.SupplierNotFound",
        "El proveedor no existe.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Suppliers.AddPaymentOrder.NoCashSessionOpen",
        "No hay una sesión de caja abierta para tu caja asignada.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Suppliers.AddPaymentOrder.NoAssignedCashDrawer",
        "No tenés una caja asignada. Pedile a un administrador que te asigne una caja para poder registrar pagos.");

    public static readonly Error CashSessionFromPreviousDay = Error.Conflict(
        "Suppliers.AddPaymentOrder.CashSessionFromPreviousDay",
        "La sesión de caja está abierta desde un día anterior. Cerrá la sesión antes de registrar pagos.");
}
