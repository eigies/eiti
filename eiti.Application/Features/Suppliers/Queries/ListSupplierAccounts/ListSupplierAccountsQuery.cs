using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Suppliers.Queries.ListSupplierAccounts;

public sealed record ListSupplierAccountsQuery(string? Search = null)
    : IRequest<Result<IReadOnlyList<SupplierAccountListItem>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesAccess];
}

public sealed record SupplierAccountListItem(
    Guid SupplierId,
    string Name,
    string? Phone,
    decimal SaldoPendiente,
    decimal SaldoAFavor);
