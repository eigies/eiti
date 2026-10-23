using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.AddCustomerReceipt;

// Los errores de cada medio (tarjeta, cheque, monto, método) están en CustomerPaymentLineErrors.
public static class AddCustomerReceiptErrors
{
    public static readonly Error CustomerNotFound = Error.NotFound(
        "Customers.AddReceipt.CustomerNotFound",
        "El cliente no existe.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Customers.AddReceipt.NoCashSessionOpen",
        "No hay una sesión de caja abierta para tu caja asignada.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Customers.AddReceipt.NoAssignedCashDrawer",
        "No tenés una caja asignada. Pedile a un administrador que te asigne una caja para poder registrar cobros.");

    public static readonly Error CashSessionFromPreviousDay = Error.Conflict(
        "Customers.AddReceipt.CashSessionFromPreviousDay",
        "La sesión de caja está abierta desde un día anterior. Cerrá la sesión antes de registrar cobros.");
}
