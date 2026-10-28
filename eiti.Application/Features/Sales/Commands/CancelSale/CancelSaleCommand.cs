using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CancelSale;

// Qué hacer con lo ya cobrado al anular una venta de cuenta corriente con cobros imputados.
public enum CcCancellationRefundMode
{
    // Lo cobrado vuelve como saldo a favor del cliente (no toca caja).
    Credit = 1,
    // Se revierte el cobro en caja (cuentas a 0; no genera saldo a favor).
    ReversePayments = 2
}

/// <param name="ConfirmInvoiceReceiver">
/// El usuario vio que la factura se emitió a otro receptor que el de la ficha actual del cliente y confirmó
/// que la nota de crédito salga al de la factura. Sin esto, con receptores distintos no se anula nada.
/// </param>
public sealed record CancelSaleCommand(
    Guid Id,
    CcCancellationRefundMode? RefundMode = null,
    bool ConfirmInvoiceReceiver = false
) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesDelete];
}
