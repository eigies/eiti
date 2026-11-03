using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Suppliers.Common;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPaymentOrder;

// Un medio de la orden. Con cheque de cartera el monto es el del cheque; con OwnCheque se emite uno propio por Amount.
public sealed record AddSupplierPaymentOrderLine(
    int Method,
    decimal Amount,
    Guid? ChequeId,
    string? Reference,
    OwnChequeInput? OwnCheque = null);

public sealed record AddSupplierPaymentOrderCommand(
    Guid SupplierId,
    DateTime Date,
    string? Notes,
    IReadOnlyList<AddSupplierPaymentOrderLine> Lines
) : IRequest<Result<AddSupplierPaymentOrderResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesPay];
}
