using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;

namespace eiti.Application.Features.Purchases.Common;

internal static class SupplierCreditApplicator
{
    // Aplica el saldo a favor del proveedor a sus compras pendientes (FIFO, más vieja primero),
    // en cascada: consume el crédito y registra un pago SupplierCredit en cada compra hasta que
    // se agota el saldo o no quedan pendientes. Lo que no alcanza a cubrir queda como saldo a favor
    // del proveedor, disponible para la próxima compra (mismo comportamiento que hoy).
    // El pago SupplierCredit es una imputación interna: NO toca la caja (el efectivo ya se movió
    // en el pago/sobrepago que generó el excedente).
    public static async Task ApplyToPendingPurchasesAsync(
        Supplier supplier,
        Guid companyId,
        IPurchaseRepository purchaseRepository,
        Guid? excludePurchaseId,
        CancellationToken cancellationToken)
    {
        if (supplier.CreditBalance <= 0)
            return;

        var pendingPurchases = await purchaseRepository.ListPendingBySupplierAsync(
            companyId, supplier.Id, cancellationToken);

        foreach (var purchase in pendingPurchases)
        {
            if (supplier.CreditBalance <= 0)
                break;

            if (excludePurchaseId.HasValue && purchase.Id == excludePurchaseId.Value)
                continue;

            var pendingAmount = purchase.PendingAmount;
            if (pendingAmount <= 0)
                continue;

            var applied = Math.Min(supplier.CreditBalance, pendingAmount);
            if (applied <= 0)
                continue;

            supplier.ConsumeCredit(applied);
            purchase.AddPayment(PurchasePayment.Create(
                PurchasePaymentMethod.SupplierCredit,
                applied,
                DateTime.UtcNow,
                null,
                "Saldo a favor aplicado automáticamente"));
        }
    }
}
