using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.GetLastClosedCashSession;

public sealed record GetLastClosedCashSessionQuery(Guid CashDrawerId) : IRequest<Result<LastClosedCashSessionResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashAccess];
}

public sealed record LastClosedCashSessionResponse(decimal SuggestedOpeningAmount);
