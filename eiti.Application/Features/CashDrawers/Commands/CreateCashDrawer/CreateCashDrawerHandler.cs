using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.CashDrawers.Common;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using MediatR;

namespace eiti.Application.Features.CashDrawers.Commands.CreateCashDrawer;

public sealed class CreateCashDrawerHandler : IRequestHandler<CreateCashDrawerCommand, Result<CashDrawerResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICompanyOnboardingRepository _companyOnboardingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCashDrawerHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICompanyOnboardingRepository companyOnboardingRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _companyOnboardingRepository = companyOnboardingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CashDrawerResponse>> Handle(CreateCashDrawerCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CashDrawerResponse>.Failure(authCheck.Error);

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), _currentUserService.CompanyId, cancellationToken);

        if (branch is null)
        {
            return Result<CashDrawerResponse>.Failure(Error.NotFound("CashDrawers.Create.BranchNotFound", "The requested branch was not found."));
        }

        if (await _cashDrawerRepository.NameExistsAsync(branch.Id, request.Name.Trim(), null, cancellationToken))
        {
            return Result<CashDrawerResponse>.Failure(Error.Conflict("CashDrawers.Create.NameAlreadyExists", "A cash drawer with the same name already exists."));
        }

        CashDrawer cashDrawer;
        try
        {
            cashDrawer = CashDrawer.Create(_currentUserService.CompanyId, branch.Id, request.Name);
        }
        catch (ArgumentException ex)
        {
            return Result<CashDrawerResponse>.Failure(Error.Validation("CashDrawers.Create.InvalidInput", ex.Message));
        }

        await _cashDrawerRepository.AddAsync(cashDrawer, cancellationToken);

        var onboarding = await _companyOnboardingRepository.GetByCompanyIdAsync(_currentUserService.CompanyId, cancellationToken);
        if (onboarding is null)
        {
            onboarding = CompanyOnboarding.CreateCompleted(_currentUserService.CompanyId);
            await _companyOnboardingRepository.AddAsync(onboarding, cancellationToken);
        }

        onboarding.MarkCashDrawerCreated();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CashDrawerResponse>.Success(new(cashDrawer.Id.Value, cashDrawer.BranchId.Value, cashDrawer.Name, cashDrawer.IsActive, cashDrawer.CreatedAt, cashDrawer.UpdatedAt, cashDrawer.AssignedUserId?.Value));
    }
}
