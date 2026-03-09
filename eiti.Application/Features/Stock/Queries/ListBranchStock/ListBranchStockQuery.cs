using eiti.Application.Common;
using eiti.Application.Features.Stock.Common;
using MediatR;

namespace eiti.Application.Features.Stock.Queries.ListBranchStock;

public sealed record ListBranchStockQuery(Guid BranchId) : IRequest<Result<IReadOnlyList<BranchProductStockResponse>>>;
