using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Sales.Commands.CreateSale;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CreateCcSale;

public sealed record CreateCcSaleCommand(
    Guid BranchId,
    Guid CustomerId,
    IReadOnlyList<CreateSaleDetailItemRequest> Details
) : IRequest<Result<CreateCcSaleResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesCreate];
}
