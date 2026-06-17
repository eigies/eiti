using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;

namespace eiti.Application.Common.Authorization;

public static class CashSessionResolutionPolicy
{
    public static async Task<CashSession?> ResolveOpenSessionAsync(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        CompanyId companyId,
        BranchId? fallbackBranchId,
        CashSession? originalSession,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not null)
        {
            var assignedDrawer = await cashDrawerRepository.GetByAssignedUserAsync(
                currentUserService.UserId,
                companyId,
                cancellationToken);

            if (assignedDrawer is not null)
            {
                return await cashSessionRepository.GetOpenByDrawerAsync(
                    assignedDrawer.Id,
                    companyId,
                    cancellationToken);
            }
        }

        var assignedBranchIds = (currentUserService.AllowedBranchIds ?? Array.Empty<Guid>())
            .Distinct()
            .ToList();

        if (assignedBranchIds.Count > 0)
        {
            if (originalSession is not null && assignedBranchIds.Contains(originalSession.BranchId.Value))
            {
                var originalOpen = await ResolveOriginalSessionAsync(
                    cashSessionRepository,
                    companyId,
                    originalSession,
                    cancellationToken);
                if (originalOpen is not null)
                {
                    return originalOpen;
                }
            }

            if (fallbackBranchId is not null && assignedBranchIds.Contains(fallbackBranchId.Value))
            {
                var fallbackSession = await cashSessionRepository.GetAnyOpenByBranchAsync(
                    fallbackBranchId,
                    companyId,
                    cancellationToken);
                if (fallbackSession is not null)
                {
                    return fallbackSession;
                }
            }

            foreach (var branchId in assignedBranchIds)
            {
                var session = await cashSessionRepository.GetAnyOpenByBranchAsync(
                    new BranchId(branchId),
                    companyId,
                    cancellationToken);
                if (session is not null)
                {
                    return session;
                }
            }

            return null;
        }

        if (originalSession is not null)
        {
            var originalOpen = await ResolveOriginalSessionAsync(
                cashSessionRepository,
                companyId,
                originalSession,
                cancellationToken);
            if (originalOpen is not null)
            {
                return originalOpen;
            }
        }

        if (fallbackBranchId is not null)
        {
            var fallbackSession = await cashSessionRepository.GetAnyOpenByBranchAsync(
                fallbackBranchId,
                companyId,
                cancellationToken);
            if (fallbackSession is not null)
            {
                return fallbackSession;
            }
        }

        return await cashSessionRepository.GetAnyOpenByCompanyAsync(companyId, cancellationToken);
    }

    private static async Task<CashSession?> ResolveOriginalSessionAsync(
        ICashSessionRepository cashSessionRepository,
        CompanyId companyId,
        CashSession originalSession,
        CancellationToken cancellationToken)
    {
        if (originalSession.Status == CashSessionStatus.Open)
        {
            return originalSession;
        }

        return await cashSessionRepository.GetOpenForBranchAsync(
            originalSession.BranchId,
            originalSession.CashDrawerId,
            companyId,
            cancellationToken);
    }
}
