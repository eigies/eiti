using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Purchases;
using MediatR;

namespace eiti.Application.Features.Purchases.Queries.ListPurchases;

public sealed record ListPurchasesQuery(
    Guid? SupplierId,
    int? Status,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<ListPurchasesResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesAccess];
}
