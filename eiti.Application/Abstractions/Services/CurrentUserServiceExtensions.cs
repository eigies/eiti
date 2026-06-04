using eiti.Application.Common;

namespace eiti.Application.Abstractions.Services;

public static class CurrentUserServiceExtensions
{
    /// <summary>
    /// Verifica que el usuario esté autenticado.
    /// </summary>
    public static Result EnsureAuthenticated(this ICurrentUserService service)
    {
        if (!service.IsAuthenticated)
        {
            return Result.Failure(
                Error.Unauthorized(
                    "Auth.NotAuthenticated",
                    "The current user is not authenticated."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Verifica que el usuario esté autenticado y tenga contexto completo (CompanyId y UserId).
    /// </summary>
    public static Result EnsureAuthenticatedWithContext(this ICurrentUserService service)
    {
        if (!service.IsAuthenticated)
        {
            return Result.Failure(
                Error.Unauthorized(
                    "Auth.NotAuthenticated",
                    "The current user is not authenticated."));
        }

        if (service.CompanyId is null || service.UserId is null)
        {
            return Result.Failure(
                Error.Unauthorized(
                    "Auth.IncompleteContext",
                    "The current user context is incomplete."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Verifica que el usuario tenga acceso a la sucursal indicada (o vea todas).
    /// </summary>
    public static Result EnsureBranchAccess(this ICurrentUserService service, Guid branchId)
    {
        if (service.CanViewAllBranches || service.AllowedBranchIds.Contains(branchId))
        {
            return Result.Success();
        }

        return Result.Failure(
            Error.Forbidden(
                "Auth.BranchForbidden",
                "No tenés acceso a la sucursal seleccionada."));
    }

    /// <summary>
    /// Filtra una lista de sucursales dejando solo las permitidas (o todas si ve todas).
    /// </summary>
    public static IEnumerable<Guid> FilterAllowedBranchIds(this ICurrentUserService service, IEnumerable<Guid> branchIds)
    {
        if (service.CanViewAllBranches)
        {
            return branchIds;
        }

        var allowed = service.AllowedBranchIds;
        return branchIds.Where(allowed.Contains);
    }

    /// <summary>
    /// Verifica que el usuario tenga un permiso específico.
    /// </summary>
    public static Result EnsureHasPermission(
        this ICurrentUserService service,
        string permission,
        string errorCode = "Auth.Forbidden",
        string errorMessage = "The current user does not have the required permission.")
    {
        if (!service.HasPermission(permission))
        {
            return Result.Failure(
                Error.Forbidden(errorCode, errorMessage));
        }

        return Result.Success();
    }
}
