using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Users;

namespace eiti.Application.Common;

// Reversa de un cobro de cuenta corriente, compartida entre "anular cobro" y "anular venta (anular pagos)".
// Des-imputa las ventas que financió (vuelven a pendiente; stock a reservado si estaban pagas), revierte el
// saldo a favor que generó, registra la reversa en la caja recibida y anula el cheque si aplica. NO hace
// SaveChanges — lo hace el handler. La resolución de la caja (y sus errores) queda en cada caller.
internal static class CustomerPaymentReversal
{
    public static async Task ReverseAsync(
        CustomerPayment payment,
        Customer customer,
        CashSession? session,
        ISaleRepository saleRepository,
        IBranchProductStockRepository stockRepository,
        IStockMovementRepository stockMovementRepository,
        ICustomerRepository customerRepository,
        IChequeRepository chequeRepository,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        // Revierte las imputaciones FIFO que generó este cobro (sus ventas vuelven a pendiente).
        var sales = await saleRepository.ListByCustomerPaymentIdAsync(companyId, payment.Id, cancellationToken);
        var imputedTotal = 0m;
        foreach (var sale in sales)
        {
            imputedTotal += sale.CcPayments
                .Where(p => p.CustomerPaymentId == payment.Id && p.Status == SaleCcPaymentStatus.Active)
                .Sum(p => p.Amount);

            var revertedFromPaid = sale.RevertCustomerCredit(payment.Id);

            if (revertedFromPaid)
            {
                foreach (var detail in sale.Details)
                {
                    var stock = await stockRepository.GetOrCreateAsync(
                        sale.BranchId,
                        detail.ProductId,
                        companyId,
                        cancellationToken);

                    stock.RevertSaleOut(detail.Quantity);
                    await stockMovementRepository.AddAsync(
                        StockMovement.Create(
                            companyId,
                            sale.BranchId,
                            stock.ProductId,
                            stock.Id,
                            StockMovementType.Reserve,
                            detail.Quantity,
                            "Sale",
                            sale.Id.Value,
                            "Stock reverted to reserved (customer payment cancelled).",
                            userId),
                        cancellationToken);
                }
            }
        }

        // Si el cobro generó saldo a favor, la anulación lo revierte.
        var creditGeneratedByPayment = Math.Max(0m, payment.Amount - imputedTotal);
        if (creditGeneratedByPayment > 0m)
        {
            customer.ConsumeCredit(creditGeneratedByPayment);
        }
        customerRepository.Update(customer);

        // Reversa de caja: el efectivo sale del cajón (Out); el resto reversa neutra (None) para trazabilidad.
        session?.RegisterCustomerPaymentCancellation(payment.Method, payment.Amount, payment.Id, userId);

        // El cheque recibido sale de cartera (Anulado).
        if (payment.Method == SalePaymentMethod.Check && payment.ChequeId.HasValue)
        {
            var cheque = await chequeRepository.GetByIdAsync(payment.ChequeId.Value, companyId, cancellationToken);
            if (cheque is not null && cheque.Estado == ChequeStatus.EnCartera)
                cheque.TransitionTo(ChequeStatus.Anulado);
        }

        payment.Cancel();
    }
}
