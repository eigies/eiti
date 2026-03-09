using eiti.Application.Common;
using eiti.Application.Features.Branches.Common;
using MediatR;

namespace eiti.Application.Features.Branches.Commands.UpdateBranch;

public sealed record UpdateBranchCommand(
    Guid Id,
    string Name,
    string? Code,
    string? Address
) : IRequest<Result<BranchResponse>>;
