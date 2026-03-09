namespace eiti.Application.Features.Onboarding.Common;

public sealed record OnboardingStatusResponse(
    bool HasCreatedBranch,
    bool HasCreatedCashDrawer,
    bool HasCompletedInitialCashOpen,
    bool HasCreatedProduct,
    bool HasLoadedInitialStock,
    bool IsCompleted,
    string NextStep
);
