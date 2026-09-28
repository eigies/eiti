using eiti.Application.Abstractions.Data;
using eiti.Domain.Sales;

namespace eiti.Application.Features.Sales.Common;

/// <summary>
/// Facturación al crear una venta, igual para mostrador y cuenta corriente: se factura si el usuario
/// lo pidió o si la empresa/sucursal factura sola.
/// </summary>
public static class SaleInvoicingFlow
{
    /// <summary>
    /// La venta YA está guardada cuando se llama: la facturación nunca la bloquea ni la revierte. Si el
    /// servicio fiscal falla, el comprobante queda Rechazado o en trámite y se reintenta desde la venta.
    /// Null cuando no correspondía facturar.
    /// </summary>
    public static async Task<SaleInvoicingOutcome?> TryInvoiceAsync(
        ISaleInvoicingService invoicingService,
        IUnitOfWork unitOfWork,
        Sale sale,
        bool requestInvoicing,
        CancellationToken cancellationToken)
    {
        if (!invoicingService.IsEnabled || sale.SaleStatus == SaleStatus.Cancel)
        {
            return null;
        }

        var shouldInvoice = requestInvoicing ||
            await invoicingService.IsAutomaticAsync(sale.CompanyId, sale.BranchId, cancellationToken);

        if (!shouldInvoice)
        {
            return null;
        }

        var outcome = await invoicingService.InvoiceAsync(sale, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return outcome;
    }
}
