using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.CashSessionsReport;

public sealed record CashSessionsReportQuery(
    DateTime DateFrom,
    DateTime DateTo,
    Guid? BranchId = null
) : IRequest<Result<CashSessionsReportResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ReportsCash];
}
