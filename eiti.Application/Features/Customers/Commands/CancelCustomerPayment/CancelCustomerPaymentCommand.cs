using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerPayment;

public sealed record CancelCustomerPaymentCommand(
    Guid CustomerId,
    Guid PaymentId
) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesPay];
}
