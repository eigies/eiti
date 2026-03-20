using eiti.Application.Common.Authorization;
using eiti.Domain.Users;

namespace eiti.Application.Features.Auth.Common;

public static class AuthenticationMapper
{
    public static (IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions) MapRolesAndPermissions(User user)
    {
        var roles = user.Roles.Select(role => role.RoleCode).ToArray();
        var permissions = RoleCatalog.PermissionsFor(roles).OrderBy(permission => permission).ToArray();

        return (roles, permissions);
    }
}
