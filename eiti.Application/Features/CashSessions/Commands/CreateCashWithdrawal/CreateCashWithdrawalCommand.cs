using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using MediatR;

namespace eiti.Application.Features.CashSessions.Commands.CreateCashWithdrawal;

public sealed record CreateCashWithdrawalCommand(
    Guid Id,
    decimal Amount,
    string Description
) : IRequest<Result<CashSessionResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashWithdraw];
}
