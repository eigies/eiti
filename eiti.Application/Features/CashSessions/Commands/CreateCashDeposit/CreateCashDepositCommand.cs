using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using MediatR;

namespace eiti.Application.Features.CashSessions.Commands.CreateCashDeposit;

public sealed record CreateCashDepositCommand(
    Guid Id,
    decimal Amount,
    string Description
) : IRequest<Result<CashSessionResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashWithdraw];
}
