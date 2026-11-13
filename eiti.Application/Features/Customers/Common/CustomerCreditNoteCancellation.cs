using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Users;

namespace eiti.Application.Features.Customers.Common;

// Anula una nota de crédito de cliente: deshace SOLO sus propias imputaciones (las de un cobro o las de otra NC
// quedan intactas), devuelve al saldo lo que imputó, le saca lo que aportó la nota y registra la anulación en caja
// como movimiento neutro. Una venta que la nota había saldado vuelve a pendiente y su stock, a reservado.
// Compartida entre "anular NC" y "anular la venta con sus NC".
//
// En dos pasos para que el caller chequee el saldo ANTES de tocar nada: PrepareAsync solo lee (qué imputó la nota
// y dónde) y ApplyAsync muta. NO llama SaveChanges ni Update del cliente: eso queda a cargo del handler.
//
// No reutiliza CustomerPaymentReversal: esa reintegra efectivo al cajón y devuelve el cheque a cartera, y una NC
// no movió ni una cosa ni la otra.
internal sealed class CustomerCreditNoteCancellation
{
    private readonly IReadOnlyList<Sale> _sales;

    private CustomerCreditNoteCancellation(CustomerCreditNote note, IReadOnlyList<Sale> sales, decimal imputedTotal)
    {
        Note = note;
        _sales = sales;
        ImputedTotal = imputedTotal;
    }

    public CustomerCreditNote Note { get; }

    // Lo que la nota llegó a imputar con su back-link. Se mide antes de revertir: después esas filas quedan
    // canceladas y ya no suman.
    public decimal ImputedTotal { get; }

    // Cuánto cambia el saldo a favor al anularla: vuelve lo imputado y sale lo que aportó la nota. La resta ingenua
    // (Amount − ImputedTotal) destruía el saldo que el cliente ya tenía: el applicator imputa todo el saldo, no
    // solo el de la nota, así que lo imputado con su back-link puede superar su importe.
    public decimal CreditDelta => ImputedTotal - Note.Amount;

    public static async Task<CustomerCreditNoteCancellation> PrepareAsync(
        CustomerCreditNote note,
        ISaleRepository saleRepository,
        CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var sales = await saleRepository.ListByCreditNoteIdAsync(companyId, note.Id, cancellationToken);
        var imputedTotal = sales
            .SelectMany(s => s.CcPayments)
            .Where(p => p.CreditNoteId == note.Id && p.Status == SaleCcPaymentStatus.Active)
            .Sum(p => p.Amount);

        return new CustomerCreditNoteCancellation(note, sales, imputedTotal);
    }

    public async Task ApplyAsync(
        Customer customer,
        CashSession session,
        IBranchProductStockRepository stockRepository,
        IStockMovementRepository stockMovementRepository,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        foreach (var sale in _sales)
        {
            if (!sale.RevertCreditNote(Note.Id))
                continue;

            // La venta volvió de Paid a pendiente: el stock vuelve a reservado.
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
                        "Stock reverted to reserved (credit note cancelled).",
                        userId),
                    cancellationToken);
            }
        }

        if (ImputedTotal > 0m)
            customer.AddCredit(ImputedTotal);

        customer.ConsumeCredit(Note.Amount);

        session.RegisterCustomerCreditNoteCancellation(Note.Amount, Note.Id, Note.Code, userId);
        Note.Cancel(userId.Value);
    }
}
