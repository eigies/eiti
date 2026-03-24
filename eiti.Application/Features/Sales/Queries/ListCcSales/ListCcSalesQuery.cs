using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.ListCcSales;

public sealed record ListCcSalesQuery(Guid? CustomerId)
    : IRequest<Result<IReadOnlyList<ListCcSalesItemResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesAccess];
}
