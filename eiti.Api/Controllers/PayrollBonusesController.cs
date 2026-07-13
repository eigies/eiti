using eiti.Api.Extensions;
using eiti.Application.Features.Payroll.Bonuses.Commands.CancelPayrollBonus;
using eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;
using eiti.Application.Features.Payroll.Bonuses.Queries.ListPayrollBonuses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/payroll-bonuses")]
[Authorize]
public sealed class PayrollBonusesController : ControllerBase
{
    private readonly ISender _sender;

    public PayrollBonusesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? employeeId, [FromQuery] int? status, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListPayrollBonusesQuery(employeeId, status), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePayrollBonusRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreatePayrollBonusCommand(request.EmployeeId, request.ConceptId, request.AmountType, request.Value, request.Notes),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelPayrollBonusCommand(id), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record CreatePayrollBonusRequest(Guid EmployeeId, Guid ConceptId, int AmountType, decimal Value, string? Notes);
