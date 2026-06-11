using eiti.Api.Extensions;
using eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;
using eiti.Application.Features.Suppliers.Commands.CancelSupplierPayment;
using eiti.Application.Features.Suppliers.Commands.CreateSupplier;
using eiti.Application.Features.Suppliers.Commands.DeactivateSupplier;
using eiti.Application.Features.Suppliers.Commands.UpdateSupplier;
using eiti.Application.Features.Suppliers.Queries.GetSupplierAccount;
using eiti.Application.Features.Suppliers.Queries.ListSupplierAccounts;
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

    // ── Cuenta corriente / bolsa del proveedor ───────────────────────────────

    [HttpGet("accounts")]
    public async Task<IActionResult> ListSupplierAccounts(
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ListSupplierAccountsQuery(search), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}/account")]
    public async Task<IActionResult> GetSupplierAccount(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSupplierAccountQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> AddSupplierPayment(
        Guid id,
        [FromBody] AddSupplierPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AddSupplierPaymentCommand(id, request.Method, request.Amount, request.Date, request.Reference, request.Notes, request.ChequeId),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}/payments/{paymentId:guid}")]
    public async Task<IActionResult> CancelSupplierPayment(
        Guid id,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelSupplierPaymentCommand(id, paymentId), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record AddSupplierPaymentRequest(
    int Method,
    decimal Amount,
    DateTime Date,
    string? Reference,
    string? Notes,
    Guid? ChequeId = null);
