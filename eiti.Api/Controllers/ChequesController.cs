using eiti.Api.Extensions;
using eiti.Application.Features.Cheques.Commands.UpdateChequeStatus;
using eiti.Application.Features.Cheques.Queries.GetChequeById;
using eiti.Application.Features.Cheques.Queries.ListCheques;
using eiti.Domain.Cheques;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/cheques")]
[Authorize]
public sealed class ChequesController : ControllerBase
{
    private readonly ISender _sender;

    public ChequesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? estado,
        [FromQuery] int? bankId,
        [FromQuery] DateTime? fechaVencFrom,
        [FromQuery] DateTime? fechaVencTo,
        CancellationToken cancellationToken)
    {
        ChequeStatus? chequeStatus = estado.HasValue && Enum.IsDefined(typeof(ChequeStatus), estado.Value)
            ? (ChequeStatus)estado.Value
            : null;

        var result = await _sender.Send(
            new ListChequesQuery(chequeStatus, bankId, fechaVencFrom, fechaVencTo),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetChequeByIdQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateChequeStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateChequeStatusCommand(id, request.NewStatus), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record UpdateChequeStatusRequest(int NewStatus);
