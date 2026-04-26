using eiti.Application.Common;
using eiti.Application.Features.CashDrawers.Common;
using MediatR;

namespace eiti.Application.Features.CashDrawers.Queries.ListCashDrawers;

public sealed record ListCashDrawersQuery(Guid BranchId) : IRequest<Result<IReadOnlyList<CashDrawerResponse>>>;
