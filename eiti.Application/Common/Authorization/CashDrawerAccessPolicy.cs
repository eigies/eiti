using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Cash;

namespace eiti.Application.Common.Authorization;

public static class CashDrawerAccessPolicy
{
    public static async Task<CashDrawer?> GetRestrictedAssignedDrawerAsync(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        CancellationToken cancellationToken)
    {
        if (currentUserService.CompanyId is null
            || currentUserService.UserId is null
            || currentUserService.HasPermission(PermissionCodes.CashDrawerViewAll))
        {
            return null;
        }

        return await cashDrawerRepository.GetByAssignedUserAsync(
            currentUserService.UserId,
            currentUserService.CompanyId,
            cancellationToken);
    }

    public static async Task<Result> EnsureCanAccessDrawerAsync(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        CashDrawerId cashDrawerId,
        CancellationToken cancellationToken)
    {
        var assignedDrawer = await GetRestrictedAssignedDrawerAsync(currentUserService, cashDrawerRepository, cancellationToken);
        if (assignedDrawer is null || assignedDrawer.Id == cashDrawerId)
        {
            return Result.Success();
        }

        return Result.Failure(
            Error.Forbidden(
                "CashDrawers.Access.Forbidden",
                "The current user cannot operate a cash drawer other than the assigned one."));
    }

    public static async Task<Guid?> ResolveEffectiveDrawerIdAsync(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        Guid? requestedCashDrawerId,
        CancellationToken cancellationToken)
    {
        var assignedDrawer = await GetRestrictedAssignedDrawerAsync(currentUserService, cashDrawerRepository, cancellationToken);
        return assignedDrawer?.Id.Value ?? requestedCashDrawerId;
    }
}
