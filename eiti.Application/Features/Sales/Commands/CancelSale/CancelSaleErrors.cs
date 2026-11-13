using System.Globalization;
using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.CancelSale;

public static class CancelSaleErrors
{
    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

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

    // Sin la confirmación del usuario no se anula nada. El front muestra este mensaje (nombra cada nota y su importe)
    // y, si confirma, reenvía con cancelLinkedCreditNotes=true.
    public static Error HasActiveCreditNote(IReadOnlyCollection<(string Code, decimal Amount)> notes) => Error.Conflict(
        "Sales.Cancel.HasActiveCreditNote",
        notes.Count == 1
            ? $"La venta tiene vigente la nota de crédito {Describe(notes)}. Si confirmás, se anula junto con la venta: lo que imputó vuelve a quedar pendiente y su crédito sale del saldo a favor del cliente."
            : $"La venta tiene vigentes las notas de crédito {Describe(notes)}. Si confirmás, se anulan junto con la venta: lo que imputaron vuelve a quedar pendiente y su crédito sale del saldo a favor del cliente.");

    // Anular las notas saca su crédito del saldo a favor; si ya se usó sin su back-link y lo que devuelve la venta
    // no alcanza, el saldo quedaría negativo.
    public static Error InsufficientCreditForCreditNotes(IReadOnlyCollection<string> codes, decimal missing) => Error.Conflict(
        "Sales.Cancel.InsufficientCreditForCreditNotes",
        $"No se anuló nada: anular la venta con {(codes.Count == 1 ? "la nota de crédito" : "las notas de crédito")} {string.Join(", ", codes)} dejaría el saldo a favor del cliente en negativo (faltan {Money(missing)}). Su crédito ya se usó y lo que devuelve la anulación de la venta no alcanza para cubrirlo.");

    // "Anular los pagos" sin NC: lo que un cobro dejó a favor ya lo consumió otra venta sin back-link al cobro, y la
    // reversa dejaría el saldo negativo. "Saldo a favor" siempre se puede: devuelve lo cobrado sin tocar los cobros.
    public static Error CreditAlreadyConsumed(decimal missing) => Error.Conflict(
        "Sales.Cancel.CreditAlreadyConsumed",
        $"No se anuló nada: anular los pagos dejaría el saldo a favor del cliente en negativo (faltan {Money(missing)}), porque lo que esos cobros dejaron a favor ya se usó en otras ventas. Podés anular la venta eligiendo \"Saldo a favor\".");

    // Anular una nota registra un movimiento (neutro) en la caja, igual que anularla sola desde la cuenta del cliente.
    public static readonly Error CreditNoteNoCashSessionOpen = Error.Conflict(
        "Sales.Cancel.CreditNoteNoCashSessionOpen",
        "Para anular la nota de crédito junto con la venta necesitás una caja abierta: la anulación queda registrada en la caja.");

    public static readonly Error CreditNoteNoAssignedCashDrawer = Error.Conflict(
        "Sales.Cancel.CreditNoteNoAssignedCashDrawer",
        "Para anular la nota de crédito junto con la venta necesitás una caja asignada (o permiso para ver todas las cajas).");

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

    private static string Describe(IEnumerable<(string Code, decimal Amount)> notes) =>
        string.Join(", ", notes.Select(note => $"{note.Code} por {Money(note.Amount)}"));

    private static string Money(decimal amount) => amount.ToString("$ #,##0.00", Argentina);
}
