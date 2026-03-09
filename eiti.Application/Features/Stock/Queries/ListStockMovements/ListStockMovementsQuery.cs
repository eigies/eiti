using eiti.Application.Common;
using eiti.Application.Features.Stock.Common;
using MediatR;

namespace eiti.Application.Features.Stock.Queries.ListStockMovements;

public sealed record ListStockMovementsQuery(Guid BranchId, Guid ProductId) : IRequest<Result<IReadOnlyList<StockMovementResponse>>>;
