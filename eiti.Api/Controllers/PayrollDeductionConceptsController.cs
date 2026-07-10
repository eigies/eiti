using eiti.Api.Extensions;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.SetDeductionConceptActive;
using eiti.Application.Features.Payroll.DeductionConcepts.Commands.UpdateDeductionConcept;
using eiti.Application.Features.Payroll.DeductionConcepts.Queries.ListDeductionConcepts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/payroll-deduction-concepts")]
[Authorize]
public sealed class PayrollDeductionConceptsController : ControllerBase
{
    private readonly ISender _sender;

    public PayrollDeductionConceptsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool activeOnly, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListDeductionConceptsQuery(activeOnly), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeductionConceptRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateDeductionConceptCommand(request.Name, request.Percentage), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDeductionConceptRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateDeductionConceptCommand(id, request.Name, request.Percentage), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetDeductionConceptActiveRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetDeductionConceptActiveCommand(id, request.IsActive), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record CreateDeductionConceptRequest(string Name, decimal Percentage);
public sealed record UpdateDeductionConceptRequest(string Name, decimal Percentage);
public sealed record SetDeductionConceptActiveRequest(bool IsActive);
