using eiti.Api.Extensions;
using eiti.Application.Features.Customers.Queries.ListCustomerAccounts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/customer-accounts")]
[Authorize]
public sealed class CustomerAccountsController : ControllerBase
{
    private readonly ISender _sender;

    public CustomerAccountsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> ListCustomerAccounts(
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ListCustomerAccountsQuery(search), cancellationToken);
        return result.ToActionResult();
    }
}
