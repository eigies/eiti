using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CreateSale;

public sealed record CreateSaleCommand(
    Guid BranchId,
    Guid? CustomerId,
    int IdSaleStatus,
    bool HasDelivery,
    Guid? CashDrawerId,
    IReadOnlyList<CreateSaleDetailItemRequest> Details
) : IRequest<Result<CreateSaleResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesCreate];
}

public sealed record CreateSaleDetailItemRequest(
    Guid ProductId,
    int Quantity);
