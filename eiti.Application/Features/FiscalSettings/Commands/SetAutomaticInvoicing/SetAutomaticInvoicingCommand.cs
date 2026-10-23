using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Commands.SetAutomaticInvoicing;

/// <summary>Con la opción activa cada venta se factura al confirmarse; si no, se elige venta por venta.</summary>
public sealed record SetAutomaticInvoicingCommand(bool Enabled) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesInvoice];
}
