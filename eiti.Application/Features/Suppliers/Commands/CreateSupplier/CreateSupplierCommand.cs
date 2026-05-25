using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CreateSupplier;

public sealed record CreateSupplierCommand(
    string Name,
    string? Phone,
    string? Email,
    string? TaxId,
    string? Notes
) : IRequest<Result<CreateSupplierResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SuppliersManage];
}
