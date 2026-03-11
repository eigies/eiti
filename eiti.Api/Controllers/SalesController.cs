using eiti.Api.Extensions;
using eiti.Application.Features.SaleTransport;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Application.Features.Sales.Commands.DeleteSale;
using eiti.Application.Features.Sales.Commands.SendSaleWhatsApp;
using eiti.Application.Features.Sales.Commands.UpdateSale;
using eiti.Application.Features.Sales.Queries.ListSales;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SalesController : ControllerBase
{
    private readonly ISender _sender;

    public SalesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateSale(
        [FromBody] CreateSaleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> ListSales(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int? idSaleStatus,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ListSalesQuery(dateFrom, dateTo, idSaleStatus),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSale(
        Guid id,
        [FromBody] UpdateSaleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSale(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteSaleCommand(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}/transport")]
    public async Task<IActionResult> GetTransport(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSaleTransportQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/transport")]
    public async Task<IActionResult> CreateTransport(Guid id, [FromBody] CreateSaleTransportCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { SaleId = id }, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/transport")]
    public async Task<IActionResult> UpdateTransport(Guid id, [FromBody] UpdateSaleTransportCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { SaleId = id }, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/transport/status")]
    public async Task<IActionResult> UpdateTransportStatus(Guid id, [FromBody] UpdateSaleTransportStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateSaleTransportStatusCommand(id, request.Status), cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}/transport")]
    public async Task<IActionResult> DeleteTransport(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteSaleTransportCommand(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/send-whatsapp")]
    public async Task<IActionResult> SendSaleWhatsApp(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SendSaleWhatsAppCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record UpdateSaleTransportStatusRequest(int Status);

