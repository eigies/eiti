using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.GetCustomerPaymentLink;

public sealed record GetCustomerPaymentLinkQuery(Guid PaymentId)
    : IRequest<Result<GetCustomerPaymentLinkResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashAccess];
}

public sealed record GetCustomerPaymentLinkResponse(
    Guid PaymentId,
    Guid CustomerId,
    decimal Amount,
    int Method,
    int Status,
    DateTime Date);
