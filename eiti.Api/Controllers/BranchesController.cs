using eiti.Api.Extensions;
using eiti.Application.Features.Branches.Commands.CreateBranch;
using eiti.Application.Features.Branches.Commands.DeleteBranch;
using eiti.Application.Features.Branches.Commands.UpdateBranch;
using eiti.Application.Features.Branches.Queries.ListBranches;
using eiti.Application.Features.Branches.Queries.ListTransferTargets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class BranchesController : ControllerBase
{
    private readonly ISender _sender;

    public BranchesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListBranchesQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("transfer-targets")]
    public async Task<IActionResult> ListTransferTargets(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListTransferTargetsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBranchCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBranchCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteBranchCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}
