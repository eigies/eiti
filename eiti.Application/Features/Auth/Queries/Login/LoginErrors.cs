using eiti.Application.Common;

namespace eiti.Application.Features.Auth.Queries.Login;

public static class LoginErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Auth.Login.InvalidCredentials",
        "Invalid username/email or password.");
}
