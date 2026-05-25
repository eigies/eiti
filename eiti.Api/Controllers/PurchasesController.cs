using eiti.Api.Extensions;
using eiti.Application.Features.Purchases.Commands.AddPurchasePayment;
using eiti.Application.Features.Purchases.Commands.CancelPurchase;
using eiti.Application.Features.Purchases.Commands.CancelPurchasePayment;
using eiti.Application.Features.Purchases.Commands.CreatePurchase;
using eiti.Application.Features.Purchases.Queries.GetPurchaseById;
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

    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> AddPurchasePayment(
        Guid id,
        [FromBody] AddPurchasePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AddPurchasePaymentCommand(id, request.Method, request.Amount, request.Date, request.Reference, request.Notes),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}/payments/{paymentId:guid}")]
    public async Task<IActionResult> CancelPurchasePayment(
        Guid id,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelPurchasePaymentCommand(id, paymentId), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record AddPurchasePaymentRequest(
    int Method,
    decimal Amount,
    DateTime Date,
    string? Reference,
    string? Notes);
