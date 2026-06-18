using eiti.Api.Extensions;
using eiti.Application.Features.Customers.Commands.AddCustomerPayment;
using eiti.Application.Features.Customers.Commands.CancelCustomerPayment;
using eiti.Application.Features.Customers.Commands.CreateCustomer;
using eiti.Application.Features.Customers.Commands.DeleteCustomer;
using eiti.Application.Features.Customers.Commands.UpdateCustomer;
using eiti.Application.Features.Customers.Queries.GetCustomerAccount;
using eiti.Application.Features.Customers.Queries.GetCustomerById;
using eiti.Application.Features.Customers.Queries.GetCustomerPaymentLink;
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCustomer(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteCustomerCommand(id), cancellationToken);
        return result.ToActionResult();
    }

    // ── Cuenta corriente / bolsa del cliente ─────────────────────────────────

    [HttpGet("payments/{paymentId:guid}/link")]
    public async Task<IActionResult> GetCustomerPaymentLink(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerPaymentLinkQuery(paymentId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}/account")]
    public async Task<IActionResult> GetCustomerAccount(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerAccountQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> AddCustomerPayment(
        Guid id,
        [FromBody] AddCustomerPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AddCustomerPaymentCommand(
                id,
                request.Method,
                request.Amount,
                request.Date,
                request.Reference,
                request.Notes,
                request.CardBankId,
                request.CardCuotas,
                request.Cheque),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}/payments/{paymentId:guid}")]
    public async Task<IActionResult> CancelCustomerPayment(
        Guid id,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelCustomerPaymentCommand(id, paymentId), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record AddCustomerPaymentRequest(
    int Method,
    decimal Amount,
    DateTime Date,
    string? Reference,
    string? Notes,
    int? CardBankId = null,
    int? CardCuotas = null,
    AddCustomerPaymentChequeData? Cheque = null);
