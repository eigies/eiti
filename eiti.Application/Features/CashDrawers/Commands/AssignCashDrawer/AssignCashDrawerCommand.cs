using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.CashDrawers.Commands.AssignCashDrawer;

public sealed record AssignCashDrawerCommand(Guid CashDrawerId, Guid? UserId)
    : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashDrawerAssign];
}
