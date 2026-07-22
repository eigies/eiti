using eiti.Api.Extensions;
using eiti.Application.Features.Quotes.Commands.CancelQuote;
using eiti.Application.Features.Quotes.Commands.ConvertQuoteToSale;
using eiti.Application.Features.Quotes.Commands.CreateQuote;
using eiti.Application.Features.Quotes.Queries.GetQuoteById;
using eiti.Application.Features.Quotes.Queries.ListQuotes;
using eiti.Domain.Quotes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class QuotesController : ControllerBase
{
    private readonly ISender _sender;

    public QuotesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateQuote(
        [FromBody] CreateQuoteCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> ListQuotes(
        [FromQuery] int? idQuoteStatus,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? customerId,
        CancellationToken cancellationToken)
    {
        QuoteStatus? status = idQuoteStatus.HasValue ? (QuoteStatus)idQuoteStatus.Value : null;
        var result = await _sender.Send(new ListQuotesQuery(status, dateFrom, dateTo, customerId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetQuoteById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetQuoteByIdQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelQuote(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelQuoteCommand(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/convert")]
    public async Task<IActionResult> ConvertQuoteToSale(
        Guid id,
        [FromBody] ConvertQuoteToSaleRequestBody body,
        CancellationToken cancellationToken)
    {
        var command = new ConvertQuoteToSaleCommand(
            id, body.BranchId, body.CustomerId, body.Details, body.TradeIns,
            body.GeneralDiscountPercent, body.ManualOverridePrice, body.WithVat);
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record ConvertQuoteToSaleRequestBody(
    Guid BranchId,
    Guid CustomerId,
    IReadOnlyList<eiti.Application.Features.Sales.Commands.CreateSale.CreateSaleDetailItemRequest> Details,
    IReadOnlyList<eiti.Application.Features.Sales.Commands.CreateSale.CreateSaleTradeInItemRequest>? TradeIns,
    decimal GeneralDiscountPercent,
    decimal? ManualOverridePrice,
    bool WithVat = false);
