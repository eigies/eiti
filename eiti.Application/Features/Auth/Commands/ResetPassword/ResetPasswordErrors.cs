using eiti.Application.Common;

namespace eiti.Application.Features.Auth.Commands.ResetPassword;

public static class ResetPasswordErrors
{
    public static readonly Error InvalidOrExpiredCode = Error.Validation(
        "Auth.ResetPassword.InvalidOrExpiredCode",
        "El código es inválido o ha expirado.");
}
