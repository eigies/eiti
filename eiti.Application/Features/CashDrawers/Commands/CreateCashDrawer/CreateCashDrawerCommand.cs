using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashDrawers.Common;
using MediatR;

namespace eiti.Application.Features.CashDrawers.Commands.CreateCashDrawer;

public sealed record CreateCashDrawerCommand(
    Guid BranchId,
    string Name
) : IRequest<Result<CashDrawerResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashDrawerManage];
}
