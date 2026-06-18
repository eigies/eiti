using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.ListCustomerAccounts;

public sealed record ListCustomerAccountsQuery(string? Search = null)
    : IRequest<Result<ListCustomerAccountsResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesAccess];
}

public sealed record ListCustomerAccountsResponse(
    IReadOnlyList<CustomerAccountListItem> Items);

public sealed record CustomerAccountListItem(
    Guid CustomerId,
    string Name,
    string? Phone,
    string? DocumentNumber,
    string? TaxId,
    decimal SaldoPendiente,
    decimal SaldoAFavor);
