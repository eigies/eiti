using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.UpdateSupplier;

public sealed record UpdateSupplierCommand(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? TaxId,
    string? Notes
) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SuppliersManage];
}
