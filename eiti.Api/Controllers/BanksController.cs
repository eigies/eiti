using eiti.Api.Extensions;
using eiti.Application.Features.Banks.Commands.CreateBank;
using eiti.Application.Features.Banks.Commands.UpdateBank;
using eiti.Application.Features.Banks.Commands.UpsertInstallmentPlan;
using eiti.Application.Features.Banks.Queries.ListBanks;
using eiti.Domain.Banks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/banks")]
[Authorize]
public sealed class BanksController : ControllerBase
{
    private readonly ISender _sender;

    public BanksController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool activeOnly,
        [FromQuery] BankUsage usage = BankUsage.All,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ListBanksQuery(activeOnly, usage), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBankRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateBankCommand(
                request.Name,
                request.UseForCard ?? true,
                request.UseForTransfer ?? true,
                request.UseForCheque ?? true),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBankRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateBankCommand(
                id,
                request.Name,
                request.Active,
                request.UseForCard,
                request.UseForTransfer,
                request.UseForCheque),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:int}/plans")]
    public async Task<IActionResult> UpsertPlan(int id, [FromBody] UpsertInstallmentPlanRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpsertInstallmentPlanCommand(id, request.Cuotas, request.SurchargePct, request.Active), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record CreateBankRequest(
    string Name,
    bool? UseForCard = null,
    bool? UseForTransfer = null,
    bool? UseForCheque = null);

public sealed record UpdateBankRequest(
    string Name,
    bool Active,
    bool? UseForCard = null,
    bool? UseForTransfer = null,
    bool? UseForCheque = null);

public sealed record UpsertInstallmentPlanRequest(int Cuotas, decimal SurchargePct, bool Active);
