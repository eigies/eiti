using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Purchases.Commands.CancelPurchase;

// Qué hacer con lo ya pagado al anular una compra con pagos imputados.
public enum PurchaseCancellationRefundMode
{
    // Lo pagado queda como saldo a favor del proveedor (no toca caja) y se auto-aplica FIFO a compras pendientes.
    Credit = 1,
    // Se revierte el/los pago(s) que financiaron la compra (reintegra caja en efectivo, devuelve cheque a cartera).
    ReversePayments = 2
}

public sealed record CancelPurchaseCommand(
    Guid Id,
    PurchaseCancellationRefundMode? RefundMode = null
) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesCancel];
}
