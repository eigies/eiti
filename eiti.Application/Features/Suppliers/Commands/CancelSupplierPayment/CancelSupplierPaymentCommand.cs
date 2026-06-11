using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierPayment;

public sealed record CancelSupplierPaymentCommand(
    Guid SupplierId,
    Guid PaymentId
) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesCancel];
}
