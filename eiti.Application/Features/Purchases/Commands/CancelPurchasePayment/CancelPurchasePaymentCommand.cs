using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Purchases.Commands.CancelPurchasePayment;

public sealed record CancelPurchasePaymentCommand(
    Guid PurchaseId,
    Guid PaymentId
) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesPay];
}
