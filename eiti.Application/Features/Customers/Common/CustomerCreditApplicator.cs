using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Sales;

namespace eiti.Application.Features.Customers.Common;

internal static class CustomerCreditApplicator
{
    // Aplica el saldo a favor del cliente a sus ventas CC pendientes (FIFO, más vieja primero), en cascada:
    // consume el crédito y registra una imputación interna (SaleCcPayment método CustomerCredit con back-link al
    // cobro) en cada venta hasta que se agota el saldo o no quedan pendientes. Lo que no alcanza a cubrir queda
    // como saldo a favor del cliente, disponible para la próxima venta (mismo comportamiento que proveedores).
    // La imputación CustomerCredit NO toca la caja (el efectivo ya se movió en el cobro que generó el excedente).
    // Devuelve el detalle de qué ventas cubrió y por cuánto (FIFO) y los SaleId que pasaron a Paid (para confirmar stock).
    public static async Task<CustomerCreditApplicationResult> ApplyToPendingCcSalesAsync(
        Customer customer,
        CompanyId companyId,
        ISaleRepository saleRepository,
        CancellationToken cancellationToken,
        Guid? customerPaymentId = null,
        Guid? creditNoteId = null)
    {
        var imputaciones = new List<CustomerPaymentImputacion>();
        var salesNowPaid = new List<Guid>();
        var salesNowPaidEntities = new List<Sale>();

        if (customer.CreditBalance <= 0)
            return new CustomerCreditApplicationResult(imputaciones, salesNowPaid, salesNowPaidEntities);

        var pendingSales = await saleRepository.ListPendingCcSalesByCustomerAsync(
            companyId, customer.Id, cancellationToken);

        foreach (var sale in pendingSales)
        {
            if (customer.CreditBalance <= 0)
                break;

            var pendingAmount = sale.CcPendingAmount;
            if (pendingAmount <= 0)
                continue;

            var applied = Math.Min(customer.CreditBalance, pendingAmount);
            if (applied <= 0)
                continue;

            customer.ConsumeCredit(applied);
            var becamePaid = sale.ApplyCustomerCredit(
                applied,
                DateTime.UtcNow,
                customerPaymentId ?? Guid.Empty,
                "Saldo a favor aplicado automáticamente",
                creditNoteId);

            imputaciones.Add(new CustomerPaymentImputacion(sale.Id.Value, sale.Code ?? string.Empty, applied));

            if (becamePaid)
            {
                salesNowPaid.Add(sale.Id.Value);
                // La entidad ya viene tracked y con Details cargados (ListPendingCcSalesByCustomerAsync):
                // el handler confirma stock con ella, sin re-fetch.
                salesNowPaidEntities.Add(sale);
            }
        }

        return new CustomerCreditApplicationResult(imputaciones, salesNowPaid, salesNowPaidEntities);
    }
}

public sealed record CustomerCreditApplicationResult(
    IReadOnlyList<CustomerPaymentImputacion> Imputaciones,
    IReadOnlyList<Guid> SalesNowPaid,
    IReadOnlyList<Sale> SalesNowPaidEntities);
