using eiti.Application.Common.Authorization;
using eiti.Domain.Users;

namespace eiti.Application.Features.Auth.Common;

public static class AuthenticationMapper
{
    public static IReadOnlyList<string> MapPermissions(User user) =>
        user.AccessProfile.Permissions
            .Select(p => p.PermissionCode)
            .OrderBy(p => p)
            .ToArray();

    public static bool CanViewAllBranches(User user, IReadOnlyList<string> permissions) =>
        permissions.Contains(PermissionCodes.BranchesViewAll) || user.BranchAccessIds.Count == 0;
}
