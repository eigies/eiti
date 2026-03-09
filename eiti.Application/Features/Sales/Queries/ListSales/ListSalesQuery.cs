using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.ListSales;

public sealed record ListSalesQuery(
    DateTime? DateFrom,
    DateTime? DateTo,
    int? IdSaleStatus
) : IRequest<Result<IReadOnlyList<ListSalesItemResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesAccess];
}
