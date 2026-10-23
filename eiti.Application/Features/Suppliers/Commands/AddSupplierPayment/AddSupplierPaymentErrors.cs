using eiti.Application.Common;
using eiti.Application.Features.Suppliers.Common;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;

public static class AddSupplierPaymentErrors
{
    public static readonly Error SupplierNotFound = Error.NotFound(
        "Suppliers.AddPayment.SupplierNotFound",
        "El proveedor no existe.");

    public static readonly Error InvalidPaymentMethod = SupplierPaymentLineErrors.InvalidPaymentMethod;

    public static readonly Error InvalidAmount = SupplierPaymentLineErrors.InvalidAmount;

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Suppliers.AddPayment.NoCashSessionOpen",
        "No hay una sesión de caja abierta para tu caja asignada.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Suppliers.AddPayment.NoAssignedCashDrawer",
        "No tenés una caja asignada. Pedile a un administrador que te asigne una caja para poder registrar pagos.");

    public static readonly Error CashSessionFromPreviousDay = Error.Conflict(
        "Suppliers.AddPayment.CashSessionFromPreviousDay",
        "La sesión de caja está abierta desde un día anterior. Cerrá la sesión antes de registrar pagos.");

    public static readonly Error ChequeRequired = SupplierPaymentLineErrors.ChequeRequired;

    public static readonly Error ChequeNotFound = SupplierPaymentLineErrors.ChequeNotFound;

    public static readonly Error ChequeNotEnCartera = SupplierPaymentLineErrors.ChequeNotEnCartera;
}
