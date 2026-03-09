using eiti.Api.Extensions;
using eiti.Application.Features.Onboarding.Commands.CompleteInitialCashOpen;
using eiti.Application.Features.Onboarding.Queries.GetOnboardingStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class OnboardingController : ControllerBase
{
    private readonly ISender _sender;

    public OnboardingController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetOnboardingStatusQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("complete-initial-cash-open")]
    public async Task<IActionResult> CompleteInitialCashOpen([FromBody] CompleteInitialCashOpenCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }
}
