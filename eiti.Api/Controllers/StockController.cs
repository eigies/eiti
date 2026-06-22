using eiti.Api.Extensions;
using eiti.Application.Features.Stock.Commands.AdjustStock;
using eiti.Application.Features.Stock.Commands.ImportBranchPricing;
using eiti.Application.Features.Stock.Commands.SetBranchProductPricing;
using eiti.Application.Features.Stock.Commands.TransferStock;
using eiti.Application.Features.Stock.Queries.GetBranchProductStock;
using eiti.Application.Features.Stock.Queries.GetProductReservations;
using eiti.Application.Features.Stock.Queries.GetTransferDetail;
using eiti.Application.Features.Stock.Queries.ListBranchStock;
using eiti.Application.Features.Stock.Queries.ListStockMovements;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StockController : ControllerBase
{
    private readonly ISender _sender;

    public StockController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust(
        [FromBody] AdjustStockCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("pricing")]
    public async Task<IActionResult> SetPricing(
        [FromBody] SetBranchProductPricingCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("pricing/import")]
    public async Task<IActionResult> ImportPricing(
        [FromBody] ImportBranchPricingCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer(
        [FromBody] TransferStockCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> ListByBranch(
        [FromQuery] Guid branchId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListBranchStockQuery(branchId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> GetByProduct(
        Guid productId,
        [FromQuery] Guid branchId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetBranchProductStockQuery(productId, branchId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("transfer/{referenceId:guid}")]
    public async Task<IActionResult> GetTransferDetail(
        Guid referenceId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTransferDetailQuery(referenceId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("reservations")]
    public async Task<IActionResult> ListReservations(
        [FromQuery] Guid productId,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetProductReservationsQuery(productId, branchId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("movements")]
    public async Task<IActionResult> ListMovements(
        [FromQuery] Guid branchId,
        [FromQuery] Guid productId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListStockMovementsQuery(branchId, productId), cancellationToken);
        return result.ToActionResult();
    }
}
