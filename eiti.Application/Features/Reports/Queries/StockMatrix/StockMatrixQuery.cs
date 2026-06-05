using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.StockMatrix;

public sealed record StockMatrixQuery()
    : IRequest<Result<StockMatrixResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ReportsStock];
}
