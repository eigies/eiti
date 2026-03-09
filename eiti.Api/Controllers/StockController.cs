using eiti.Api.Extensions;
using eiti.Application.Features.Stock.Commands.AdjustStock;
using eiti.Application.Features.Stock.Queries.GetBranchProductStock;
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
