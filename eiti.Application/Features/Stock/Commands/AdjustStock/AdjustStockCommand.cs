using eiti.Application.Common;
using eiti.Application.Features.Stock.Common;
using MediatR;

namespace eiti.Application.Features.Stock.Commands.AdjustStock;

public sealed record AdjustStockCommand(
    Guid BranchId,
    Guid ProductId,
    int Quantity,
    int Type,
    string? Description
) : IRequest<Result<BranchProductStockResponse>>;
