using eiti.Api.Extensions;
using eiti.Application.Features.Payroll.Advances.Commands.CancelPayrollAdvance;
using eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;
using eiti.Application.Features.Payroll.Advances.Queries.ListPayrollAdvances;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/payroll-advances")]
[Authorize]
public sealed class PayrollAdvancesController : ControllerBase
{
    private readonly ISender _sender;

    public PayrollAdvancesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? employeeId, [FromQuery] int? status, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListPayrollAdvancesQuery(employeeId, status), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePayrollAdvanceRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreatePayrollAdvanceCommand(request.EmployeeId, request.Amount, request.Date, request.Notes, request.PaymentMethod, request.CashSessionId),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelPayrollAdvanceCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record CreatePayrollAdvanceRequest(Guid EmployeeId, decimal Amount, DateTime Date, string? Notes, int PaymentMethod, Guid? CashSessionId);
