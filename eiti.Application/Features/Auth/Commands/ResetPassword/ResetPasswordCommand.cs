using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(
    string Email,
    string Code,
    string NewPassword
) : IRequest<Result>;
