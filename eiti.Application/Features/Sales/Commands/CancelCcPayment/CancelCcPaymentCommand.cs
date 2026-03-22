using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CancelCcPayment;

public sealed record CancelCcPaymentCommand(
    Guid SaleId,
    Guid PaymentId
) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesPay];
}
