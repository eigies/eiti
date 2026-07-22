using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using eiti.Domain.Users;

namespace eiti.Application.Features.Purchases.Common;

// Revierte un pago de proveedor COMPLETO, reutilizado por "anular pago" y por "anular compra (revertir el pago)".
// Deshace las imputaciones FIFO que generó el pago (sus compras vuelven a Pendiente), revierte el saldo a favor
// que haya generado, reintegra la caja (solo efectivo) con la sesión provista y devuelve el cheque a cartera.
// NO llama SaveChanges ni Update del proveedor: eso queda a cargo del handler.
internal static class SupplierPaymentReversal
{
    public static async Task ReverseAsync(
        SupplierPayment payment,
        Supplier supplier,
        CashSession? session,
        IPurchaseRepository purchaseRepository,
        IChequeRepository chequeRepository,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        // Revertir las imputaciones FIFO que generó este pago (sus compras vuelven a Pendiente).
        var purchases = await purchaseRepository.ListBySupplierPaymentIdAsync(companyId.Value, payment.Id, cancellationToken);
        var imputedTotal = 0m;
        foreach (var purchase in purchases)
        {
            var rows = purchase.Payments
                .Where(p => p.SupplierPaymentId == payment.Id && p.Status == PurchasePaymentStatus.Active)
                .ToList();
            foreach (var row in rows)
            {
                imputedTotal += row.Amount;
                purchase.CancelPayment(row.Id);
            }
        }

        // Si el pago generó saldo a favor, la anulación lo revierte.
        var creditGeneratedByPayment = Math.Max(0m, payment.Amount - imputedTotal);
        if (creditGeneratedByPayment > 0m)
            supplier.ConsumeCredit(creditGeneratedByPayment);

        // Reintegro de caja (solo efectivo) y devolución del cheque a cartera.
        session?.RegisterSupplierPaymentCancel(payment.Amount, payment.Id, userId, payment.Method);

        if (payment.Method == PurchasePaymentMethod.Check && payment.ChequeId.HasValue)
        {
            var cheque = await chequeRepository.GetByIdAsync(payment.ChequeId.Value, companyId, cancellationToken);
            if (cheque is not null && cheque.Estado == ChequeStatus.Entregado)
                cheque.ReturnToCartera();
        }

        payment.Cancel();
    }
}
