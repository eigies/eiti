using eiti.Application.Common.Authorization;
using eiti.Domain.Users;

namespace eiti.Application.Features.Auth.Common;

public static class AuthenticationMapper
{
    public static (IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions) MapRolesAndPermissions(User user)
    {
        var permissions = user.AccessProfile.Permissions
            .Select(permission => permission.PermissionCode)
            .OrderBy(permission => permission)
            .ToArray();

        return (Array.Empty<string>(), permissions);
    }
}
