using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.ListCcPayments;

public sealed record ListCcPaymentsQuery(
    Guid SaleId
) : IRequest<Result<IReadOnlyList<ListCcPaymentsItemResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesAccess];
}
