using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.DailySalesControl;

public sealed record DailySalesControlQuery(
    DateTime DateFrom,
    DateTime DateTo,
    int Status = 0
) : IRequest<Result<DailySalesControlResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [PermissionCodes.ReportsSalesDailyControl];
}
