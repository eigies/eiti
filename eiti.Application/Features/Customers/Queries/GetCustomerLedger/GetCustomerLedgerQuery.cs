using eiti.Application.Common;
using eiti.Application.Common.Accounts;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.GetCustomerLedger;

// Extracto de la cuenta corriente del cliente. DateFrom/DateTo son fechas de negocio inclusivas (null = sin límite).
public sealed record GetCustomerLedgerQuery(
    Guid CustomerId,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    bool IncludeCancelled = false
) : IRequest<Result<AccountLedgerResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesAccess];
}
