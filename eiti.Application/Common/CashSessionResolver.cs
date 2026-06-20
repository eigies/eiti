using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common.Authorization;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Users;

namespace eiti.Application.Common;

public enum CashSessionResolveStatus
{
    Resolved,
    NoAssignedDrawer,
    NoSessionOpen
}

/// <summary>
/// Resuelve la caja (CashSession) abierta para el usuario actual. Centraliza el patrón
/// "caja propia (drawer asignado) o, con permiso CashDrawerViewAll, cualquier caja abierta de la empresa"
/// que estaba duplicado en los handlers de cobro/pago y de anulación.
/// Devuelve un status para que cada handler lo mapee a su propio Error y preserve el contrato de su endpoint.
/// </summary>
public static class CashSessionResolver
{
    public static async Task<(CashSession? Session, CashSessionResolveStatus Status)> ResolveOpenSessionAsync(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        UserId userId,
        CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var assignedDrawer = await cashDrawerRepository.GetByAssignedUserAsync(userId, companyId, cancellationToken);
        if (assignedDrawer is not null)
        {
            var session = await cashSessionRepository.GetOpenByDrawerAsync(assignedDrawer.Id, companyId, cancellationToken);
            return session is null
                ? (null, CashSessionResolveStatus.NoSessionOpen)
                : (session, CashSessionResolveStatus.Resolved);
        }

        if (currentUserService.HasPermission(PermissionCodes.CashDrawerViewAll))
        {
            var session = await cashSessionRepository.GetAnyOpenByCompanyAsync(companyId, cancellationToken);
            return session is null
                ? (null, CashSessionResolveStatus.NoSessionOpen)
                : (session, CashSessionResolveStatus.Resolved);
        }

        return (null, CashSessionResolveStatus.NoAssignedDrawer);
    }

    /// <summary>
    /// Variante best-effort para reversas de no-efectivo: intenta resolver una caja abierta
    /// (propia o, con CashDrawerViewAll, cualquiera) sin fallar si no hay ninguna.
    /// </summary>
    public static async Task<CashSession?> ResolveOpenSessionBestEffortAsync(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        UserId userId,
        CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var assignedDrawer = await cashDrawerRepository.GetByAssignedUserAsync(userId, companyId, cancellationToken);
        if (assignedDrawer is not null)
            return await cashSessionRepository.GetOpenByDrawerAsync(assignedDrawer.Id, companyId, cancellationToken);

        if (currentUserService.HasPermission(PermissionCodes.CashDrawerViewAll))
            return await cashSessionRepository.GetAnyOpenByCompanyAsync(companyId, cancellationToken);

        return null;
    }
}
