using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.ListCashSessionHistory;

public sealed record ListCashSessionHistoryQuery(
    Guid CashDrawerId,
    DateTime? From = null,
    DateTime? To = null) : IRequest<Result<IReadOnlyList<CashSessionResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashAccess];
}
