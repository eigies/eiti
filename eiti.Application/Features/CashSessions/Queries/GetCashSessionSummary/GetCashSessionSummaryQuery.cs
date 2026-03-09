using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.GetCashSessionSummary;

public sealed record GetCashSessionSummaryQuery(Guid Id) : IRequest<Result<CashSessionSummaryResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.CashAccess];
}
