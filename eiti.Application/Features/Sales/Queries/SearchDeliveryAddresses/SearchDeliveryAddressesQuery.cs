using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.SearchDeliveryAddresses;

public sealed record SearchDeliveryAddressesQuery(string Query) : IRequest<Result<IReadOnlyList<string>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesAccess];
}
