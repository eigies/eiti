using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.FiscalSettings.Common;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Queries.GetFiscalSettings;

public sealed record GetFiscalSettingsQuery : IRequest<Result<FiscalSettingsResponse>>, IRequirePermissions
{
    // Quien factura es quien configura la facturación (en la práctica, el dueño).
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesInvoice];
}
