using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Branches.Queries.ListTransferTargets;

public sealed record ListTransferTargetsQuery()
    : IRequest<Result<IReadOnlyList<TransferTargetResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.StockTransfer];
}

public sealed record TransferTargetResponse(Guid Id, string Name);
