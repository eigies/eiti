using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Suppliers.Queries.ListSuppliers;

public sealed record ListSuppliersQuery(
    string? Search,
    bool ActiveOnly = true
) : IRequest<Result<List<ListSuppliersResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesAccess];
}
