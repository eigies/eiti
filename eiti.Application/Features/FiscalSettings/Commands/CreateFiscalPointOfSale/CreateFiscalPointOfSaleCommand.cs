using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.FiscalSettings.Common;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Commands.CreateFiscalPointOfSale;

/// <summary>Carga un punto de venta que el contador dio de alta en ARCA y, opcionalmente, lo ata a una sucursal.</summary>
public sealed record CreateFiscalPointOfSaleCommand(int Number, Guid? BranchId)
    : IRequest<Result<FiscalPointOfSaleResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesInvoice];
}
