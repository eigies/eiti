using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.CustomerDebtors;

public sealed record CustomerDebtorsQuery
    : IRequest<Result<CustomerDebtorsResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ReportsDebtors];
}
