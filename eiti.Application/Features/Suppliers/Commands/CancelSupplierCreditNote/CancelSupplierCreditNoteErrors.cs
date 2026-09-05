using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierCreditNote;

public static class CancelSupplierCreditNoteErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Suppliers.CancelCreditNote.NotFound",
        "La nota de crédito no existe.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Suppliers.CancelCreditNote.AlreadyCancelled",
        "La nota de crédito ya está anulada.");

    public static readonly Error CreditAlreadyConsumed = Error.Conflict(
        "Suppliers.CancelCreditNote.CreditAlreadyConsumed",
        "El saldo a favor generado por esta nota de crédito ya fue utilizado y no se puede revertir.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Suppliers.CancelCreditNote.NoCashSessionOpen",
        "No hay una sesión de caja abierta para registrar la anulación.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Suppliers.CancelCreditNote.NoAssignedCashDrawer",
        "No tenés una caja asignada para registrar la anulación.");
}
