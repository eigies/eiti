using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Onboarding.Common;
using eiti.Domain.Companies;
using MediatR;

namespace eiti.Application.Features.Onboarding.Queries.GetOnboardingStatus;

public sealed class GetOnboardingStatusHandler : IRequestHandler<GetOnboardingStatusQuery, Result<OnboardingStatusResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyOnboardingRepository _companyOnboardingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public GetOnboardingStatusHandler(
        ICurrentUserService currentUserService,
        ICompanyOnboardingRepository companyOnboardingRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _companyOnboardingRepository = companyOnboardingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<OnboardingStatusResponse>> Handle(GetOnboardingStatusQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<OnboardingStatusResponse>.Failure(authCheck.Error);

        var onboarding = await _companyOnboardingRepository.GetByCompanyIdAsync(_currentUserService.CompanyId, cancellationToken);

        if (onboarding is null)
        {
            onboarding = CompanyOnboarding.CreateCompleted(_currentUserService.CompanyId);
            await _companyOnboardingRepository.AddAsync(onboarding, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<OnboardingStatusResponse>.Success(Map(onboarding));
    }

    internal static OnboardingStatusResponse Map(CompanyOnboarding onboarding)
    {
        var isCompleted = onboarding.CompletedAt.HasValue;
        var nextStep = isCompleted
            ? "Done"
            : !onboarding.HasCreatedBranch
                ? "Branch"
                : !onboarding.HasCreatedCashDrawer
                    ? "CashDrawer"
                    : !onboarding.HasCompletedInitialCashOpen
                        ? "InitialCashOpen"
                        : !onboarding.HasCreatedProduct
                            ? "Product"
                            : !onboarding.HasLoadedInitialStock
                                ? "Stock"
                            : "Done";

        return new OnboardingStatusResponse(
            onboarding.HasCreatedBranch,
            onboarding.HasCreatedCashDrawer,
            onboarding.HasCompletedInitialCashOpen,
            onboarding.HasCreatedProduct,
            onboarding.HasLoadedInitialStock,
            isCompleted,
            nextStep);
    }
}
