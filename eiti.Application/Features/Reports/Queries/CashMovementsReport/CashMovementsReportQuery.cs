using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.CashMovementsReport;

public sealed record CashMovementsReportQuery(
    DateTime DateFrom,
    DateTime DateTo
) : IRequest<Result<CashMovementsReportResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ReportsCash];
}
