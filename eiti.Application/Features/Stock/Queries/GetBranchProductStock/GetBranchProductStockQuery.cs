using eiti.Application.Common;
using eiti.Application.Features.Stock.Common;
using MediatR;

namespace eiti.Application.Features.Stock.Queries.GetBranchProductStock;

public sealed record GetBranchProductStockQuery(Guid ProductId, Guid BranchId) : IRequest<Result<BranchProductStockResponse>>;
