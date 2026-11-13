using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Cash;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using eiti.Domain.Users;

namespace eiti.Application.Features.Purchases.Common;

// Anula una nota de crédito de proveedor: deshace SOLO sus propias imputaciones (las de un pago o las de otra NC
// quedan intactas), devuelve al saldo lo que imputó, le saca lo que aportó la nota y registra la anulación en caja
// como movimiento neutro. Compartida entre "anular NC" y "anular la compra con sus NC".
//
// En dos pasos para que el caller chequee el saldo ANTES de tocar nada: PrepareAsync solo lee (qué imputó la nota
// y dónde) y Apply muta. NO llama SaveChanges ni Update del proveedor: eso queda a cargo del handler.
//
// No reutiliza SupplierPaymentReversal: esa reintegra efectivo al cajón y devuelve el cheque a cartera, y una NC no
// movió ni una cosa ni la otra.
internal sealed class SupplierCreditNoteCancellation
{
    private readonly IReadOnlyList<Purchase> _purchases;

    private SupplierCreditNoteCancellation(SupplierCreditNote note, IReadOnlyList<Purchase> purchases, decimal imputedTotal)
    {
        Note = note;
        _purchases = purchases;
        ImputedTotal = imputedTotal;
    }

    public SupplierCreditNote Note { get; }

    // Lo que la nota llegó a imputar con su back-link. Se mide antes de revertir: después esas filas quedan
    // canceladas y ya no suman.
    public decimal ImputedTotal { get; }

    // Cuánto cambia el saldo a favor al anularla: vuelve lo imputado y sale lo que aportó la nota. La resta ingenua
    // (Amount − ImputedTotal) destruía el saldo que el proveedor ya tenía: el applicator imputa todo el saldo, no
    // solo el de la nota, así que lo imputado con su back-link puede superar su importe.
    public decimal CreditDelta => ImputedTotal - Note.Amount;

    public static async Task<SupplierCreditNoteCancellation> PrepareAsync(
        SupplierCreditNote note,
        IPurchaseRepository purchaseRepository,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var purchases = await purchaseRepository.ListByCreditNoteIdAsync(companyId, note.Id, cancellationToken);
        var imputedTotal = purchases
            .SelectMany(p => p.Payments)
            .Where(p => p.CreditNoteId == note.Id && p.Status == PurchasePaymentStatus.Active)
            .Sum(p => p.Amount);

        return new SupplierCreditNoteCancellation(note, purchases, imputedTotal);
    }

    public void Apply(Supplier supplier, CashSession session, UserId userId)
    {
        foreach (var purchase in _purchases)
        {
            purchase.RevertCreditNote(Note.Id);
        }

        if (ImputedTotal > 0m)
            supplier.AddCredit(ImputedTotal);

        supplier.ConsumeCredit(Note.Amount);

        session.RegisterSupplierCreditNoteCancellation(Note.Amount, Note.Id, Note.Code, userId);
        Note.Cancel(userId.Value);
    }
}
