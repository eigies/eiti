using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Companies.Commands.SetRetailDiscountLimit;

/// <summary>
/// Descuento máximo (%) que cualquier vendedor puede aplicar en una venta minorista. 0 lo deshabilita.
/// Comando propio (no UpdateCurrentCompany) porque solo lo puede tocar el dueño.
/// </summary>
public sealed record SetRetailDiscountLimitCommand(decimal MaxPercent) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesDiscountConfigure];
}
