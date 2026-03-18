using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Branches.Common;
using eiti.Domain.Branches;
using MediatR;

namespace eiti.Application.Features.Branches.Commands.UpdateBranch;

public sealed class UpdateBranchHandler : IRequestHandler<UpdateBranchCommand, Result<BranchResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBranchHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BranchResponse>> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BranchResponse>.Failure(authCheck.Error);

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.Id), _currentUserService.CompanyId, cancellationToken);

        if (branch is null)
        {
            return Result<BranchResponse>.Failure(Error.NotFound("Branches.Update.NotFound", "The requested branch was not found."));
        }

        if (await _branchRepository.NameExistsAsync(_currentUserService.CompanyId, request.Name.Trim(), branch.Id, cancellationToken))
        {
            return Result<BranchResponse>.Failure(Error.Conflict("Branches.Update.NameAlreadyExists", "A branch with the same name already exists."));
        }

        try
        {
            branch.Update(request.Name, request.Code, request.Address);
        }
        catch (ArgumentException ex)
        {
            return Result<BranchResponse>.Failure(Error.Validation("Branches.Update.InvalidInput", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BranchResponse>.Success(new(branch.Id.Value, branch.Name, branch.Code, branch.Address, 0, 0m, branch.CreatedAt, branch.UpdatedAt));
    }
}
