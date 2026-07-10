using eiti.Api.Extensions;
using eiti.Application.Features.Payroll.Liquidations.CancelLiquidation;
using eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;
using eiti.Application.Features.Payroll.Liquidations.GetLiquidationById;
using eiti.Application.Features.Payroll.Liquidations.ListLiquidations;
using eiti.Application.Features.Payroll.Liquidations.PayLiquidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/payroll-liquidations")]
[Authorize]
public sealed class PayrollLiquidationsController : ControllerBase
{
    private readonly ISender _sender;

    public PayrollLiquidationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? employeeId,
        [FromQuery] string? periodLabel,
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ListLiquidationsQuery(employeeId, periodLabel, status, page, pageSize), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetLiquidationByIdQuery(id), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GeneratePayrollPeriodRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GeneratePayrollPeriodCommand(request.Periodicity, request.PeriodLabel, request.PeriodStart, request.PeriodEnd),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid id, [FromBody] PayLiquidationRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new PayLiquidationCommand(id, request.PaymentMethod, request.CashSessionId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelLiquidationCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record GeneratePayrollPeriodRequest(int Periodicity, string PeriodLabel, DateTime PeriodStart, DateTime PeriodEnd);
public sealed record PayLiquidationRequest(int PaymentMethod, Guid? CashSessionId);
