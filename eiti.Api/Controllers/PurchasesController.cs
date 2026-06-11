using eiti.Api.Extensions;
using eiti.Application.Features.Purchases.Commands.CancelPurchase;
using eiti.Application.Features.Purchases.Commands.CreatePurchase;
using eiti.Application.Features.Purchases.Queries.GetPurchaseById;
using eiti.Application.Features.Purchases.Queries.ListCarteraCheques;
using eiti.Application.Features.Purchases.Queries.ListPurchases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PurchasesController : ControllerBase
{
    private readonly ISender _sender;

    public PurchasesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> ListPurchases(
        [FromQuery] Guid? supplierId,
        [FromQuery] int? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new ListPurchasesQuery(supplierId, status, from, to, page, pageSize),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> CreatePurchase(
        [FromBody] CreatePurchaseCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPurchaseById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPurchaseByIdQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> CancelPurchase(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelPurchaseCommand(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("cartera-cheques")]
    public async Task<IActionResult> ListCarteraCheques(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListCarteraChequesQuery(), cancellationToken);
        return result.ToActionResult();
    }
}
