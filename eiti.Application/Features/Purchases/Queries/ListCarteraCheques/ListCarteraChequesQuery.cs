using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Purchases.Queries.ListCarteraCheques;

public sealed record ListCarteraChequesQuery
    : IRequest<Result<IReadOnlyList<CarteraChequeResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesPay];
}
