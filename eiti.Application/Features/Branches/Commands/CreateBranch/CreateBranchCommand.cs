using eiti.Application.Common;
using eiti.Application.Features.Branches.Common;
using MediatR;

namespace eiti.Application.Features.Branches.Commands.CreateBranch;

public sealed record CreateBranchCommand(
    string Name,
    string? Code,
    string? Address
) : IRequest<Result<BranchResponse>>;
