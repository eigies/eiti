using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using MediatR;

namespace eiti.Application.Features.CashSessions.Commands.CloseCashSession;

public sealed record CloseCashSessionCommand(
    Guid Id,
    decimal ActualClosingAmount,
    string? Notes
) : IRequest<Result<CashSessionResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashClose];
}
