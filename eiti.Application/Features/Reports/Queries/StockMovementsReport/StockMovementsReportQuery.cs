using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.StockMovementsReport;

public sealed record StockMovementsReportQuery(
    DateTime DateFrom,
    DateTime DateTo,
    Guid? ProductId = null,
    Guid? BranchId = null,
    int? Type = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<Result<StockMovementsReportResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ReportsStock];
}
