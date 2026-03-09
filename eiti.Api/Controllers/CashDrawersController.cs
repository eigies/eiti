using eiti.Api.Extensions;
using eiti.Application.Features.CashDrawers.Commands.CreateCashDrawer;
using eiti.Application.Features.CashDrawers.Commands.UpdateCashDrawer;
using eiti.Application.Features.CashDrawers.Queries.ListCashDrawers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CashDrawersController : ControllerBase
{
    private readonly ISender _sender;

    public CashDrawersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid branchId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListCashDrawersQuery(branchId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCashDrawerCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCashDrawerCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return result.ToActionResult();
    }
}
