using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Stock.Commands.TransferStock;

public sealed record TransferStockItemRequest(
    Guid ProductId,
    int Quantity);

public sealed record TransferStockCommand(
    Guid SourceBranchId,
    Guid DestinationBranchId,
    IReadOnlyList<TransferStockItemRequest> Items,
    string? Description
) : IRequest<Result<TransferStockResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.StockTransfer];
}
