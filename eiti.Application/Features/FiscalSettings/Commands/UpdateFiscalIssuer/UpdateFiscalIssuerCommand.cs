using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.FiscalSettings.Common;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Commands.UpdateFiscalIssuer;

/// <summary>Datos del emisor que van impresos en la factura. El CUIT no se edita.</summary>
public sealed record UpdateFiscalIssuerCommand(
    string LegalName,
    IvaCondition IvaCondition,
    string? Iibb,
    DateOnly ActivityStartDate,
    string? CommercialAddress)
    : IRequest<Result<FiscalIssuerResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesInvoice];
}
