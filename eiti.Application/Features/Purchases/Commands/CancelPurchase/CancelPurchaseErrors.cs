using System.Globalization;
using eiti.Application.Common;

namespace eiti.Application.Features.Purchases.Commands.CancelPurchase;

public static class CancelPurchaseErrors
{
    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

    public static readonly Error NotFound = Error.NotFound(
        "Purchases.Cancel.NotFound",
        "The purchase was not found.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Purchases.Cancel.AlreadyCancelled",
        "The purchase is already cancelled.");

    public static readonly Error SupplierNotFound = Error.NotFound(
        "Purchases.Cancel.SupplierNotFound",
        "The supplier for this purchase was not found.");

    // Sin la confirmación del usuario no se anula nada. El front muestra este mensaje (nombra cada nota y su importe)
    // y, si confirma, reenvía con cancelLinkedCreditNotes=true.
    public static Error HasActiveCreditNote(IReadOnlyCollection<(string Code, decimal Amount)> notes) => Error.Conflict(
        "Purchases.Cancel.HasActiveCreditNote",
        notes.Count == 1
            ? $"La compra tiene vigente la nota de crédito {Describe(notes)}. Si confirmás, se anula junto con la compra y su crédito sale del saldo a favor del proveedor."
            : $"La compra tiene vigentes las notas de crédito {Describe(notes)}. Si confirmás, se anulan junto con la compra y su crédito sale del saldo a favor del proveedor.");

    // Anular las notas saca su crédito del saldo a favor; si ya se usó sin su back-link y lo que devuelve la compra
    // no alcanza, el saldo quedaría negativo.
    public static Error InsufficientCreditForCreditNotes(IReadOnlyCollection<string> codes, decimal missing) => Error.Conflict(
        "Purchases.Cancel.InsufficientCreditForCreditNotes",
        $"No se anuló nada: anular la compra con {(codes.Count == 1 ? "la nota de crédito" : "las notas de crédito")} {string.Join(", ", codes)} dejaría el saldo a favor del proveedor en negativo (faltan {Money(missing)}). Su crédito ya se usó y lo que devuelve la anulación de la compra no alcanza para cubrirlo.");

    // Anular una nota registra un movimiento (neutro) en la caja, igual que anularla sola desde la cuenta del proveedor.
    public static readonly Error CreditNoteNoCashSessionOpen = Error.Conflict(
        "Purchases.Cancel.CreditNoteNoCashSessionOpen",
        "Para anular la nota de crédito junto con la compra necesitás una caja abierta: la anulación queda registrada en la caja.");

    public static readonly Error CreditNoteNoAssignedCashDrawer = Error.Conflict(
        "Purchases.Cancel.CreditNoteNoAssignedCashDrawer",
        "Para anular la nota de crédito junto con la compra necesitás una caja asignada (o permiso para ver todas las cajas).");

    public static readonly Error RefundModeRequired = Error.Validation(
        "Purchases.Cancel.RefundModeRequired",
        "La compra tiene pagos imputados. Elegí qué hacer con lo pagado: saldo a favor o revertir el pago.");

    public static readonly Error NoAssignedCashDrawer = Error.Validation(
        "Purchases.Cancel.NoAssignedCashDrawer",
        "Para revertir un pago en efectivo necesitás una caja asignada (o permiso para ver todas las cajas).");

    public static readonly Error NoCashSessionOpen = Error.Validation(
        "Purchases.Cancel.NoCashSessionOpen",
        "Para revertir un pago en efectivo necesitás una caja abierta.");

    public static readonly Error SupplierPaymentNotFound = Error.NotFound(
        "Purchases.Cancel.SupplierPaymentNotFound",
        "No se encontró el pago de proveedor asociado a la compra.");

    private static string Describe(IEnumerable<(string Code, decimal Amount)> notes) =>
        string.Join(", ", notes.Select(note => $"{note.Code} por {Money(note.Amount)}"));

    private static string Money(decimal amount) => amount.ToString("$ #,##0.00", Argentina);
}
