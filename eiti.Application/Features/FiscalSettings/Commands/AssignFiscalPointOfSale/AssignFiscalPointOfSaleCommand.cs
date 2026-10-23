using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.FiscalSettings.Common;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Commands.AssignFiscalPointOfSale;

/// <summary>Ata el punto de venta a una sucursal; BranchId null lo deja libre.</summary>
public sealed record AssignFiscalPointOfSaleCommand(Guid PointOfSaleId, Guid? BranchId)
    : IRequest<Result<FiscalPointOfSaleResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesInvoice];
}

public sealed record AssignFiscalPointOfSaleRequest(Guid? BranchId);
