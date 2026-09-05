using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerCreditNote;

public static class CancelCustomerCreditNoteErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Customers.CancelCreditNote.NotFound",
        "La nota de crédito no existe.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Customers.CancelCreditNote.AlreadyCancelled",
        "La nota de crédito ya está anulada.");

    // El crédito de la NC ya se gastó y el saldo actual no alcanza para revertirlo:
    // anularla dejaría el saldo a favor en negativo.
    public static readonly Error CreditAlreadyConsumed = Error.Conflict(
        "Customers.CancelCreditNote.CreditAlreadyConsumed",
        "El saldo a favor generado por esta nota de crédito ya fue utilizado y no se puede revertir.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Customers.CancelCreditNote.NoCashSessionOpen",
        "No hay una sesión de caja abierta para registrar la anulación.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Customers.CancelCreditNote.NoAssignedCashDrawer",
        "No tenés una caja asignada para registrar la anulación.");
}
