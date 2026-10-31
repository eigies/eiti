using eiti.Api.Extensions;
using eiti.Application.Features.Companies.Commands.SetRetailDiscountLimit;
using eiti.Application.Features.Companies.Commands.UpdateCurrentCompany;
using eiti.Application.Features.Companies.Queries.GetCurrentCompany;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CompaniesController : ControllerBase
{
    private readonly ISender _sender;

    public CompaniesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentCompany(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCurrentCompanyQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("current")]
    public async Task<IActionResult> UpdateCurrentCompany(
        [FromBody] UpdateCurrentCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    // Tope del descuento minorista: endpoint propio porque solo lo fija el dueño (sales.discount.configure).
    [HttpPut("current/retail-discount-limit")]
    public async Task<IActionResult> SetRetailDiscountLimit(
        [FromBody] SetRetailDiscountLimitCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }
}
