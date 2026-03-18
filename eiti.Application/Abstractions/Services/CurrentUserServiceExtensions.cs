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
