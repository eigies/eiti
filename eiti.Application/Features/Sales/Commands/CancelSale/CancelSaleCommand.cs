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

public sealed record CancelSaleCommand(
    Guid Id,
    CcCancellationRefundMode? RefundMode = null
) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesDelete];
}
