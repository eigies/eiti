using eiti.Application.Common;

namespace eiti.Application.Features.Auth.Commands.RefreshToken;

public static class RefreshTokenErrors
{
    public static readonly Error InvalidToken = Error.Unauthorized(
        "Auth.Refresh.InvalidToken",
        "Refresh token is invalid or expired.");
}
