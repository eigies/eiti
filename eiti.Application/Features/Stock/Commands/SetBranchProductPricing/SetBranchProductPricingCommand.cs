using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Stock.Common;
using MediatR;

namespace eiti.Application.Features.Stock.Commands.SetBranchProductPricing;

public sealed record SetBranchProductPricingCommand(
    Guid BranchId,
    Guid ProductId,
    decimal? CostOverride,
    decimal? SalePriceOverride
) : IRequest<Result<BranchProductStockResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.StockManage];
}
