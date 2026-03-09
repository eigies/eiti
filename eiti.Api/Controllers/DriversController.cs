using eiti.Api.Extensions;
using eiti.Application.Features.Drivers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DriversController : ControllerBase
{
    private readonly ISender _sender;

    public DriversController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListDriversQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{employeeId:guid}")]
    public async Task<IActionResult> Get(Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDriverQuery(employeeId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertDriverProfileCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }
}
