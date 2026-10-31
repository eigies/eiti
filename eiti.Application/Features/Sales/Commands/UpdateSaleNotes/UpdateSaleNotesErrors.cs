using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.UpdateSaleNotes;

public static class UpdateSaleNotesErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Sales.UpdateNotes.NotFound",
        "La venta no existe.");

    public static readonly Error Cancelled = Error.Conflict(
        "Sales.UpdateNotes.Cancelled",
        "No se pueden editar las observaciones de una venta anulada.");
}
