using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.GetCurrentCashSession;

public sealed record GetCurrentCashSessionQuery(Guid CashDrawerId) : IRequest<Result<CashSessionResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashAccess];
}
