using eiti.Api.Extensions;
using eiti.Application.Features.FiscalSettings.Commands.AssignFiscalPointOfSale;
using eiti.Application.Features.FiscalSettings.Commands.CreateFiscalPointOfSale;
using eiti.Application.Features.FiscalSettings.Commands.DeleteFiscalPointOfSale;
using eiti.Application.Features.FiscalSettings.Commands.SetAutomaticInvoicing;
using eiti.Application.Features.FiscalSettings.Commands.UpdateFiscalIssuer;
using eiti.Application.Features.FiscalSettings.Queries.GetFiscalSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

/// <summary>Configuración de facturación electrónica: datos del emisor y puntos de venta por sucursal.</summary>
[ApiController]
[Route("api/fiscal-settings")]
[Authorize]
public sealed class FiscalSettingsController : ControllerBase
{
    private readonly ISender _sender;

    public FiscalSettingsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetFiscalSettingsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("automatic-invoicing")]
    public async Task<IActionResult> SetAutomaticInvoicing([FromBody] SetAutomaticInvoicingCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("issuer")]
    public async Task<IActionResult> UpdateIssuer([FromBody] UpdateFiscalIssuerCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("points-of-sale")]
    public async Task<IActionResult> CreatePointOfSale([FromBody] CreateFiscalPointOfSaleCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("points-of-sale/{id:guid}/branch")]
    public async Task<IActionResult> AssignPointOfSale(Guid id, [FromBody] AssignFiscalPointOfSaleRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AssignFiscalPointOfSaleCommand(id, request.BranchId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("points-of-sale/{id:guid}")]
    public async Task<IActionResult> DeletePointOfSale(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteFiscalPointOfSaleCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}
