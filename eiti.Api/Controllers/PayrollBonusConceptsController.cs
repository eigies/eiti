using eiti.Api.Extensions;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.SetBonusConceptActive;
using eiti.Application.Features.Payroll.BonusConcepts.Commands.UpdateBonusConcept;
using eiti.Application.Features.Payroll.BonusConcepts.Queries.ListBonusConcepts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/payroll-bonus-concepts")]
[Authorize]
public sealed class PayrollBonusConceptsController : ControllerBase
{
    private readonly ISender _sender;

    public PayrollBonusConceptsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool activeOnly, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListBonusConceptsQuery(activeOnly), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBonusConceptRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateBonusConceptCommand(request.Name), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBonusConceptRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateBonusConceptCommand(id, request.Name), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetBonusConceptActiveRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetBonusConceptActiveCommand(id, request.IsActive), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record CreateBonusConceptRequest(string Name);
public sealed record UpdateBonusConceptRequest(string Name);
public sealed record SetBonusConceptActiveRequest(bool IsActive);
