using eiti.Application.Common;
using eiti.Application.Common.Accounts;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Suppliers.Queries.GetSupplierLedger;

// Extracto de la cuenta del proveedor. DateFrom/DateTo son fechas de negocio inclusivas (null = sin límite).
public sealed record GetSupplierLedgerQuery(
    Guid SupplierId,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    bool IncludeCancelled = false
) : IRequest<Result<AccountLedgerResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesAccess];
}
