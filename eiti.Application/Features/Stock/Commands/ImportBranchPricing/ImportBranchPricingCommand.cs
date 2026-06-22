using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Stock.Commands.ImportBranchPricing;

public sealed record ImportBranchPricingCommand(
    IReadOnlyList<ImportBranchPricingRowRequest> Rows
) : IRequest<Result<ImportBranchPricingResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.StockManage];
}

public sealed record ImportBranchPricingRowRequest(
    string Code,
    string BranchName,
    decimal? CostOverride,
    decimal? SalePriceOverride);
