using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.UpdateSale;

public sealed record UpdateSaleCommand(
    Guid Id,
    Guid? CustomerId,
    int IdSaleStatus,
    bool HasDelivery,
    Guid? CashDrawerId,
    IReadOnlyList<UpdateSaleDetailItemRequest> Details,
    IReadOnlyList<UpdateSalePaymentItemRequest> Payments,
    IReadOnlyList<UpdateSaleTradeInItemRequest> TradeIns
) : IRequest<Result<UpdateSaleResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesUpdate];
}

public sealed record UpdateSaleDetailItemRequest(
    Guid ProductId,
    int Quantity);

public sealed record UpdateSalePaymentItemRequest(
    int IdPaymentMethod,
    decimal Amount,
    string? Reference);

public sealed record UpdateSaleTradeInItemRequest(
    Guid ProductId,
    int Quantity,
    decimal Amount);
