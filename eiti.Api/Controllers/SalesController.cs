using eiti.Api.Extensions;
using eiti.Application.Features.SaleTransport;
using eiti.Application.Features.Sales.Commands.AddCcPayment;
using eiti.Application.Features.Sales.Commands.AddCcPaymentGroup;
using eiti.Application.Features.Sales.Commands.CancelCcPayment;
using eiti.Application.Features.Sales.Commands.CancelSale;
using eiti.Application.Features.Sales.Commands.CreateCcSale;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Application.Features.Sales.Commands.DeleteSale;
using eiti.Application.Features.Sales.Commands.SendSaleWhatsApp;
using eiti.Application.Features.Sales.Commands.UpdateSale;
using eiti.Application.Features.Sales.Queries.GetSaleById;
using eiti.Application.Features.Sales.Queries.ListCcPayments;
using eiti.Application.Features.Sales.Queries.ListCcSales;
using eiti.Application.Features.Sales.Queries.ListSales;
using eiti.Application.Features.Sales.Queries.SearchDeliveryAddresses;
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

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelSale(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelSaleCommand(id), cancellationToken);
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

    [HttpGet("delivery-addresses")]
    public async Task<IActionResult> DeliveryAddresses([FromQuery] string query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SearchDeliveryAddressesQuery(query ?? string.Empty), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSaleById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSaleByIdQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("cc")]
    public async Task<IActionResult> CreateCcSale(
        [FromBody] CreateCcSaleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}/cc-payments")]
    public async Task<IActionResult> ListCcPayments(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListCcPaymentsQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cc-payments")]
    public async Task<IActionResult> AddCcPayment(
        Guid id,
        [FromBody] AddCcPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AddCcPaymentCommand(id, request.IdPaymentMethod, request.Amount, request.Date, request.Notes),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cc-payments/{paymentId:guid}/cancel")]
    public async Task<IActionResult> CancelCcPayment(
        Guid id,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelCcPaymentCommand(id, paymentId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cc-payment-group")]
    public async Task<IActionResult> AddCcPaymentGroup(
        Guid id,
        [FromBody] AddCcPaymentGroupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AddCcPaymentGroupCommand(id, request.Methods, request.Date, request.Notes, request.CashDrawerId),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("cc")]
    public async Task<IActionResult> ListCcSales(
        [FromQuery] Guid? customerId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListCcSalesQuery(customerId), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record UpdateSaleTransportStatusRequest(int Status);

public sealed record AddCcPaymentRequest(
    int IdPaymentMethod,
    decimal Amount,
    DateTime Date,
    string? Notes);

public sealed record AddCcPaymentGroupRequest(
    List<CcPaymentMethodLine> Methods,
    DateTime Date,
    string? Notes,
    Guid? CashDrawerId);

