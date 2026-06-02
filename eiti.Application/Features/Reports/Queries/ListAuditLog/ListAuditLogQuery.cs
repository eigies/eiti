using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.ListAuditLog;

public sealed record ListAuditLogQuery(
    DateTime DateFrom,
    DateTime DateTo,
    Guid? UserId,
    int Page = 1,
    int PageSize = 25
) : IRequest<Result<ListAuditLogResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ReportsAudit];
}
