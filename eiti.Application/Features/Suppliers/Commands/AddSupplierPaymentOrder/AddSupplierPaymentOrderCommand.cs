using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPaymentOrder;

// Un medio de la orden. Con cheque (de cartera) el monto es el del cheque.
public sealed record AddSupplierPaymentOrderLine(
    int Method,
    decimal Amount,
    Guid? ChequeId,
    string? Reference);

public sealed record AddSupplierPaymentOrderCommand(
    Guid SupplierId,
    DateTime Date,
    string? Notes,
    IReadOnlyList<AddSupplierPaymentOrderLine> Lines
) : IRequest<Result<AddSupplierPaymentOrderResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesPay];
}
