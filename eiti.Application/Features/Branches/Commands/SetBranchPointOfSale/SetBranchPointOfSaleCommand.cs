using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Branches.Common;
using MediatR;

namespace eiti.Application.Features.Branches.Commands.SetBranchPointOfSale;

/// <summary>
/// Asigna, cambia o quita (Number = null) el punto de venta de ARCA donde factura la sucursal.
/// Va aparte de editar la sucursal para que ningún "editar" que no conozca el campo lo borre.
/// </summary>
public sealed record SetBranchPointOfSaleCommand(Guid BranchId, int? Number)
    : IRequest<Result<BranchResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.BranchesManage];
}

public sealed record SetBranchPointOfSaleRequest(int? Number);
