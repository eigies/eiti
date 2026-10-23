using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerReceipt;

public sealed record CancelCustomerReceiptCommand(
    Guid CustomerId,
    Guid ReceiptId
) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesPay];
}
