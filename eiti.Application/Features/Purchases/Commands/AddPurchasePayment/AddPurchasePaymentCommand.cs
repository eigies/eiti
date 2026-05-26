using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Purchases.Commands.AddPurchasePayment;

public sealed record AddPurchasePaymentCommand(
    Guid PurchaseId,
    int Method,
    decimal Amount,
    DateTime Date,
    string? Reference,
    string? Notes,
    decimal? IvaPct,
    decimal? IngresosBrutosPct
) : IRequest<Result<AddPurchasePaymentResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesPay];
}
