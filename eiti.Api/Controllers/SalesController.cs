using eiti.Api.Extensions;
using eiti.Application.Features.SaleTransport;
using eiti.Application.Features.Sales.Commands.CancelSale;
using eiti.Application.Features.Sales.Commands.CreateCcSale;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Application.Features.Sales.Commands.DeleteSale;
using eiti.Application.Features.Sales.Commands.InvoiceSale;
using eiti.Application.Features.Sales.Commands.RegularizeSaleCreditNote;
using eiti.Application.Features.Sales.Commands.RetrySaleCreditNote;
using eiti.Application.Features.Sales.Commands.SendSaleWhatsApp;
using eiti.Application.Features.Sales.Commands.UpdateSale;
using eiti.Application.Features.Sales.Queries.GetSaleById;
using eiti.Application.Features.Sales.Queries.GetSaleFiscalReceivers;
using eiti.Application.Features.Sales.Queries.GetSaleInvoicePdf;
using eiti.Application.Features.Sales.Queries.GetSaleInvoicePrint;
using eiti.Domain.Sales;
using eiti.Application.Features.Sales.Queries.ListCcPayments;
using eiti.Application.Features.Sales.Queries.ListCcSales;
using eiti.Application.Features.Sales.Queries.ListSales;
using eiti.Application.Features.Sales.Queries.SearchDeliveryAddresses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using eiti.Application.Features.Sales.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
        [FromQuery] bool includeCuentaCorriente = false,
        [FromQuery] string? code = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new ListSalesQuery(dateFrom, dateTo, idSaleStatus, includeCuentaCorriente, code),
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
        [FromQuery] int? refundMode,
        [FromQuery] bool confirmInvoiceReceiver,
        CancellationToken cancellationToken)
    {
        var mode = refundMode.HasValue ? (CcCancellationRefundMode?)refundMode.Value : null;
        var result = await _sender.Send(new CancelSaleCommand(id, mode, confirmInvoiceReceiver), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/invoice")]
    public async Task<IActionResult> InvoiceSale(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] InvoiceSaleRequest? body,
        CancellationToken cancellationToken)
    {
        // Sin cuerpo (como antes) la letra la decide el cliente; con cuerpo, la elegida se valida.
        var result = await _sender.Send(new InvoiceSaleCommand(id, body?.InvoiceLetter), cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Proxy del PDF del comprobante. El endpoint del servicio fiscal exige la API key de servicio,
    /// así que el navegador no puede pedirlo directo: pasa por acá, ya autenticado como usuario.
    /// </summary>
    [HttpGet("{id:guid}/invoice/pdf")]
    public async Task<IActionResult> GetInvoicePdf(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSaleInvoicePdfQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return File(result.Value.Content, "application/pdf", result.Value.FileName);
    }

    /// <summary>Datos para que el front arme el PDF de la factura con el diseño de EITI.</summary>
    [HttpGet("{id:guid}/invoice/print")]
    public async Task<IActionResult> GetInvoicePrint(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSaleInvoicePrintQuery(id, SaleFiscalDocumentKind.Invoice), cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>Datos para que el front arme el PDF de la nota de crédito que anuló la factura.</summary>
    [HttpGet("{id:guid}/credit-note/print")]
    public async Task<IActionResult> GetCreditNotePrint(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSaleInvoicePrintQuery(id, SaleFiscalDocumentKind.CreditNote), cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>Datos para que el front arme el PDF de la nota de débito que neutralizó una NC mal emitida.</summary>
    [HttpGet("{id:guid}/debit-note/print")]
    public async Task<IActionResult> GetDebitNotePrint(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSaleInvoicePrintQuery(id, SaleFiscalDocumentKind.DebitNote), cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>A quién se emitieron la factura y su NC (según el servicio fiscal) contra la ficha del cliente.</summary>
    [HttpGet("{id:guid}/fiscal-receivers")]
    public async Task<IActionResult> GetFiscalReceivers(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSaleFiscalReceiversQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>Vuelve a pedir la NC de una venta anulada cuya factura sigue vigente.</summary>
    [HttpPost("{id:guid}/credit-note")]
    public async Task<IActionResult> RetryCreditNote(
        Guid id,
        [FromQuery] bool confirmInvoiceReceiver,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RetrySaleCreditNoteCommand(id, confirmInvoiceReceiver), cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>Regulariza una NC emitida a otro receptor que el de su factura (ND + NC nueva).</summary>
    [HttpPost("{id:guid}/credit-note/regularize")]
    public async Task<IActionResult> RegularizeCreditNote(
        Guid id,
        [FromQuery] bool confirm,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RegularizeSaleCreditNoteCommand(id, confirm), cancellationToken);
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

/// <summary>Cuerpo opcional de POST /sales/{id}/invoice.</summary>
public sealed record InvoiceSaleRequest(InvoiceLetter? InvoiceLetter);
