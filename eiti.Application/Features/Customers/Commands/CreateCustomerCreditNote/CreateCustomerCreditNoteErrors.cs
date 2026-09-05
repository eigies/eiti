using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.CreateCustomerCreditNote;

public static class CreateCustomerCreditNoteErrors
{
    public static readonly Error CustomerNotFound = Error.NotFound(
        "Customers.CreateCreditNote.CustomerNotFound",
        "El cliente no existe.");

    public static readonly Error SaleNotFound = Error.NotFound(
        "Customers.CreateCreditNote.SaleNotFound",
        "La venta asociada no existe o no es de este cliente.");

    public static readonly Error SaleCancelled = Error.Conflict(
        "Customers.CreateCreditNote.SaleCancelled",
        "No se puede emitir una nota de crédito sobre una venta anulada.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Customers.CreateCreditNote.NoCashSessionOpen",
        "No hay una sesión de caja abierta para registrar la nota de crédito.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Customers.CreateCreditNote.NoAssignedCashDrawer",
        "No tenés una caja asignada para registrar la nota de crédito.");

    public static readonly Error CashSessionFromPreviousDay = Error.Conflict(
        "Customers.CreateCreditNote.CashSessionFromPreviousDay",
        "La sesión de caja abierta es de un día anterior. Cerrala antes de continuar.");
}
