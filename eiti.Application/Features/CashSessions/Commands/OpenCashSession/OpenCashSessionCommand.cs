using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using MediatR;

namespace eiti.Application.Features.CashSessions.Commands.OpenCashSession;

public sealed record OpenCashSessionCommand(
    Guid CashDrawerId,
    decimal OpeningAmount,
    string? Notes
) : IRequest<Result<CashSessionResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashOpen];
}
