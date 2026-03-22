using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.GetSaleById;

public sealed record GetSaleByIdQuery(
    Guid SaleId
) : IRequest<Result<GetSaleByIdResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesAccess];
}
