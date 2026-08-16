using eiti.Api.Extensions;
using eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;
using eiti.Application.Features.Dashboard.Queries.ListDashboardSales;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly ISender _sender;

    public DashboardController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetDashboardSummaryQuery(dateFrom, dateTo, branchId),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("sales")]
    public async Task<IActionResult> Sales(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new ListDashboardSalesQuery(dateFrom, dateTo, branchId),
            cancellationToken);
        return result.ToActionResult();
    }
}
