using eiti.Application.Common;
using eiti.Application.Features.Onboarding.Common;
using MediatR;

namespace eiti.Application.Features.Onboarding.Queries.GetOnboardingStatus;

public sealed record GetOnboardingStatusQuery() : IRequest<Result<OnboardingStatusResponse>>;
