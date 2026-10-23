using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierPaymentOrder;

public sealed record CancelSupplierPaymentOrderCommand(
    Guid SupplierId,
    Guid PaymentOrderId
) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesCancel];
}
