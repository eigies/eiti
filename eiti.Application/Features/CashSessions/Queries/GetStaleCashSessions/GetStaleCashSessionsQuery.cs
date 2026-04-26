using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.GetStaleCashSessions;

public sealed record GetStaleCashSessionsQuery(int HoursThreshold = 20)
    : IRequest<Result<IReadOnlyList<StaleCashSessionResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashAccess];
}
