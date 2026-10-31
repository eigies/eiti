using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.CancelSale;

public static class CancelSaleErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Sales.Cancel.NotFound",
        "The requested sale was not found.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Sales.Cancel.AlreadyCancelled",
        "The sale is already cancelled.");

    public static readonly Error CashSessionNotFound = Error.NotFound(
        "Sales.Cancel.CashSessionNotFound",
        "The cash session associated with this sale was not found.");

    public static readonly Error CashSessionClosed = Error.Conflict(
        "Sales.Cancel.CashSessionClosed",
        "The cash session associated with this sale is no longer open.");

    public static readonly Error NoOpenSessionForHistoricalCancel = Error.Conflict(
        "Sales.Cancel.NoOpenSessionForHistoricalCancel",
        "No hay una sesión de caja abierta para registrar la cancelación histórica.");

    public static Error HasActiveCreditNote(IReadOnlyCollection<string> codes) => Error.Conflict(
        "Sales.Cancel.HasActiveCreditNote",
        codes.Count == 1
            ? $"La venta tiene vigente la nota de crédito {codes.First()}. Anulala primero desde la cuenta corriente del cliente: si no, su crédito le queda a favor aunque la venta ya no exista."
            : $"La venta tiene vigentes las notas de crédito {string.Join(", ", codes)}. Anulalas primero desde la cuenta corriente del cliente: si no, su crédito le queda a favor aunque la venta ya no exista.");

    public static readonly Error RefundModeRequired = Error.Validation(
        "Sales.Cancel.RefundModeRequired",
        "La venta tiene cobros: indicá si vuelven como saldo a favor o si se anulan los pagos.");

    // El front reacciona a este código: consulta los receptores (GET sales/{id}/fiscal-receivers), muestra los
    // dos y reenvía la anulación con confirmInvoiceReceiver=true si el usuario confirma.
    public static readonly Error InvoiceReceiverChanged = Error.Validation(
        "Sales.Cancel.InvoiceReceiverChanged",
        "La factura se emitió a otro receptor que el de la ficha actual del cliente. La nota de crédito tiene que salir al receptor de la factura: confirmalo para anular.");

    public static readonly Error CustomerNotFound = Error.NotFound(
        "Sales.Cancel.CustomerNotFound",
        "No se encontró el cliente de la venta para acreditar el saldo a favor.");

    public static readonly Error NoOpenSessionForRefund = Error.Conflict(
        "Sales.Cancel.NoOpenSessionForRefund",
        "Para anular los pagos en efectivo necesitás una caja abierta. Abrí caja o elegí 'saldo a favor'.");
}
