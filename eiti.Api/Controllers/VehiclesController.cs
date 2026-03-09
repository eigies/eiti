using eiti.Api.Extensions;
using eiti.Application.Features.Vehicles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class VehiclesController : ControllerBase
{
    private readonly ISender _sender;

    public VehiclesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListVehiclesQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetVehicleQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVehicleCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVehicleCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/assign-driver")]
    public async Task<IActionResult> AssignDriver(Guid id, [FromBody] AssignVehicleDriverRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AssignVehicleDriverCommand(id, request.EmployeeId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/unassign-driver")]
    public async Task<IActionResult> UnassignDriver(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UnassignVehicleDriverCommand(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeactivateVehicleCommand(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/logs")]
    public async Task<IActionResult> CreateLog(Guid id, [FromBody] CreateFleetLogCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { VehicleId = id }, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}/logs")]
    public async Task<IActionResult> ListLogs(Guid id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? type, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListFleetLogsQuery(id, from, to, type), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record AssignVehicleDriverRequest(Guid EmployeeId);
