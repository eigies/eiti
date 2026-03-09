using eiti.Application.Common;

namespace eiti.Application.Features.Auth.Commands.Register;

public static class RegisterErrors
{
    public static readonly Error UsernameAlreadyExists = Error.Conflict(
        "Auth.Register.UsernameAlreadyExists",
        "A user with that username already exists.");

    public static readonly Error EmailAlreadyExists = Error.Conflict(
        "Auth.Register.EmailAlreadyExists",
        "A user with that email already exists.");
}
