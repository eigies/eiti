using eiti.Api.Extensions;
using eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;
using eiti.Application.Features.Suppliers.Commands.AddSupplierPaymentOrder;
using eiti.Application.Features.Suppliers.Commands.CancelSupplierPayment;
using eiti.Application.Features.Suppliers.Commands.CancelSupplierPaymentOrder;
using eiti.Application.Features.Suppliers.Commands.CreateSupplier;
using eiti.Application.Features.Suppliers.Commands.DeactivateSupplier;
using eiti.Application.Features.Suppliers.Commands.UpdateSupplier;
using eiti.Application.Features.Suppliers.Queries.GetSupplierAccount;
using eiti.Application.Features.Suppliers.Queries.GetSupplierLedger;
using eiti.Application.Features.Suppliers.Queries.ListSupplierAccounts;
using eiti.Application.Features.Suppliers.Queries.ListSuppliers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using eiti.Application.Features.Suppliers.Commands.CreateSupplierCreditNote;
using eiti.Application.Features.Suppliers.Commands.CancelSupplierCreditNote;

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

    // Extracto: renglones en orden cronológico con saldo acumulado + facturas pendientes.
    [HttpGet("{id:guid}/ledger")]
    public async Task<IActionResult> GetSupplierLedger(
        Guid id,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] bool includeCancelled,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSupplierLedgerQuery(id, dateFrom, dateTo, includeCancelled), cancellationToken);
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

    // Pago con varios medios (cheques de cartera, transferencia, efectivo) bajo una orden de pago.
    [HttpPost("{id:guid}/payment-orders")]
    public async Task<IActionResult> AddSupplierPaymentOrder(
        Guid id,
        [FromBody] AddSupplierPaymentOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AddSupplierPaymentOrderCommand(
                id,
                request.Date,
                request.Notes,
                request.Lines?.Select(l => new AddSupplierPaymentOrderLine(l.Method, l.Amount, l.ChequeId, l.Reference)).ToList() ?? []),
            cancellationToken);
        return result.ToActionResult();
    }

    // Anula la orden entera (todos sus medios).
    [HttpDelete("{id:guid}/payment-orders/{paymentOrderId:guid}")]
    public async Task<IActionResult> CancelSupplierPaymentOrder(
        Guid id,
        Guid paymentOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelSupplierPaymentOrderCommand(id, paymentOrderId), cancellationToken);
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

    [HttpPost("{id:guid}/credit-notes")]
    public async Task<IActionResult> CreateSupplierCreditNote(
        Guid id,
        [FromBody] CreateSupplierCreditNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateSupplierCreditNoteCommand(id, request.Amount, request.Reason, request.Date, request.PurchaseId),
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}/credit-notes/{creditNoteId:guid}")]
    public async Task<IActionResult> CancelSupplierCreditNote(
        Guid id,
        Guid creditNoteId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CancelSupplierCreditNoteCommand(id, creditNoteId), cancellationToken);

        return result.ToActionResult();
    }
}

public sealed record CreateSupplierCreditNoteRequest(
    decimal Amount,
    string Reason,
    DateTime Date,
    Guid? PurchaseId = null);

public sealed record AddSupplierPaymentRequest(
    int Method,
    decimal Amount,
    DateTime Date,
    string? Reference,
    string? Notes,
    Guid? ChequeId = null);

public sealed record AddSupplierPaymentOrderLineRequest(
    int Method,
    decimal Amount,
    Guid? ChequeId = null,
    string? Reference = null);

public sealed record AddSupplierPaymentOrderRequest(
    DateTime Date,
    string? Notes,
    IReadOnlyList<AddSupplierPaymentOrderLineRequest>? Lines);
