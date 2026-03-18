using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Branches.Common;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using MediatR;

namespace eiti.Application.Features.Branches.Commands.CreateBranch;

public sealed class CreateBranchHandler : IRequestHandler<CreateBranchCommand, Result<BranchResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly ICompanyOnboardingRepository _companyOnboardingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBranchHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ICompanyOnboardingRepository companyOnboardingRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _companyOnboardingRepository = companyOnboardingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BranchResponse>> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BranchResponse>.Failure(authCheck.Error);

        var normalizedName = request.Name.Trim();

        if (await _branchRepository.NameExistsAsync(_currentUserService.CompanyId, normalizedName, null, cancellationToken))
        {
            return Result<BranchResponse>.Failure(Error.Conflict("Branches.Create.NameAlreadyExists", "A branch with the same name already exists."));
        }

        Branch branch;
        try
        {
            branch = Branch.Create(_currentUserService.CompanyId, request.Name, request.Code, request.Address);
        }
        catch (ArgumentException ex)
        {
            return Result<BranchResponse>.Failure(Error.Validation("Branches.Create.InvalidInput", ex.Message));
        }

        await _branchRepository.AddAsync(branch, cancellationToken);

        var onboarding = await _companyOnboardingRepository.GetByCompanyIdAsync(_currentUserService.CompanyId, cancellationToken);
        if (onboarding is null)
        {
            onboarding = CompanyOnboarding.CreateCompleted(_currentUserService.CompanyId);
            await _companyOnboardingRepository.AddAsync(onboarding, cancellationToken);
        }

        onboarding.MarkBranchCreated();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BranchResponse>.Success(Map(branch));
    }

    private static BranchResponse Map(Branch branch) =>
        new(branch.Id.Value, branch.Name, branch.Code, branch.Address, 0, 0m, branch.CreatedAt, branch.UpdatedAt);
}
