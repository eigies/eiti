using eiti.Api.Extensions;
using eiti.Application.Features.Suppliers.Commands.CreateSupplier;
using eiti.Application.Features.Suppliers.Commands.DeactivateSupplier;
using eiti.Application.Features.Suppliers.Commands.UpdateSupplier;
using eiti.Application.Features.Suppliers.Queries.ListSuppliers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SuppliersController : ControllerBase
{
    private readonly ISender _sender;

    public SuppliersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> ListSuppliers(
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ListSuppliersQuery(search, activeOnly), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> CreateSupplier(
        [FromBody] CreateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSupplier(
        Guid id,
        [FromBody] UpdateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeactivateSupplier(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeactivateSupplierCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}
