using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Roles;

public sealed record RoleResponse(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<string> Permissions);

public sealed record ListRolesQuery() : IRequest<Result<IReadOnlyList<RoleResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.UsersManage];
}

public sealed class ListRolesHandler : IRequestHandler<ListRolesQuery, Result<IReadOnlyList<RoleResponse>>>
{
    public Task<Result<IReadOnlyList<RoleResponse>>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleResponse> roles = RoleCatalog.All
            .Select(role => new RoleResponse(
                role.Code,
                role.Name,
                role.Description,
                role.Permissions.OrderBy(permission => permission).ToArray()))
            .ToArray();

        return Task.FromResult(Result<IReadOnlyList<RoleResponse>>.Success(roles));
    }
}
