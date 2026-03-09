using eiti.Api.Extensions;
using eiti.Application.Features.CashSessions.Commands.CloseCashSession;
using eiti.Application.Features.CashSessions.Commands.CreateCashWithdrawal;
using eiti.Application.Features.CashSessions.Commands.OpenCashSession;
using eiti.Application.Features.CashSessions.Queries.GetCashSessionSummary;
using eiti.Application.Features.CashSessions.Queries.GetCurrentCashSession;
using eiti.Application.Features.CashSessions.Queries.ListCashSessionHistory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CashSessionsController : ControllerBase
{
    private readonly ISender _sender;

    public CashSessionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenCashSessionCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseCashSessionCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/withdrawals")]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] CreateCashWithdrawalCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current([FromQuery] Guid cashDrawerId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCurrentCashSessionQuery(cashDrawerId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] Guid cashDrawerId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListCashSessionHistoryQuery(cashDrawerId, from, to), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> Summary(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCashSessionSummaryQuery(id), cancellationToken);
        return result.ToActionResult();
    }
}
