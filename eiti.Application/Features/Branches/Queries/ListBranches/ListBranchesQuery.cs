using eiti.Application.Common;
using eiti.Application.Features.Branches.Common;
using MediatR;

namespace eiti.Application.Features.Branches.Queries.ListBranches;

public sealed record ListBranchesQuery() : IRequest<Result<IReadOnlyList<BranchResponse>>>;
