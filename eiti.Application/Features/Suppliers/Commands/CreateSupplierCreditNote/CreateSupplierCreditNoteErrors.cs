using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Commands.CreateSupplierCreditNote;

public static class CreateSupplierCreditNoteErrors
{
    public static readonly Error SupplierNotFound = Error.NotFound(
        "Suppliers.CreateCreditNote.SupplierNotFound",
        "El proveedor no existe.");

    public static readonly Error PurchaseNotFound = Error.NotFound(
        "Suppliers.CreateCreditNote.PurchaseNotFound",
        "La compra asociada no existe o no es de este proveedor.");

    public static readonly Error PurchaseCancelled = Error.Conflict(
        "Suppliers.CreateCreditNote.PurchaseCancelled",
        "No se puede registrar una nota de crédito sobre una compra anulada.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Suppliers.CreateCreditNote.NoCashSessionOpen",
        "No hay una sesión de caja abierta para registrar la nota de crédito.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Suppliers.CreateCreditNote.NoAssignedCashDrawer",
        "No tenés una caja asignada para registrar la nota de crédito.");

    public static readonly Error CashSessionFromPreviousDay = Error.Conflict(
        "Suppliers.CreateCreditNote.CashSessionFromPreviousDay",
        "La sesión de caja abierta es de un día anterior. Cerrala antes de continuar.");
}
