using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.CashSessions.Commands.CreateCashTransfer;

public sealed record CreateCashTransferCommand(
    Guid SourceCashDrawerId,
    Guid TargetCashDrawerId,
    decimal Amount,
    string Description
) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashWithdraw];
}
