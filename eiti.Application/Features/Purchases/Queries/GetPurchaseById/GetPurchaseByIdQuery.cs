using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Purchases.Queries.GetPurchaseById;

public sealed record GetPurchaseByIdQuery(Guid Id) : IRequest<Result<GetPurchaseByIdResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesAccess];
}
