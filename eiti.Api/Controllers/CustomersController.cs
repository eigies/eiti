using eiti.Api.Extensions;
using eiti.Application.Features.Customers.Commands.CreateCustomer;
using eiti.Application.Features.Customers.Commands.UpdateCustomer;
using eiti.Application.Features.Customers.Queries.GetCustomerById;
using eiti.Application.Features.Customers.Queries.SearchCustomers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CustomersController : ControllerBase
{
    private readonly ISender _sender;

    public CustomersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer(
        [FromBody] CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCustomerById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetCustomerByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> SearchCustomers(
        [FromQuery] string? query,
        [FromQuery] string? email,
        [FromQuery] string? documentNumber,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SearchCustomersQuery(query, email, documentNumber),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchCustomersAlias(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SearchCustomersQuery(query, null, null), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCustomer(
        Guid id,
        [FromBody] UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command with { Id = id }, cancellationToken);
        return result.ToActionResult();
    }
}
