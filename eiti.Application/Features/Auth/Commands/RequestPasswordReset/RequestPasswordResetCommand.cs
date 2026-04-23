using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Auth.Commands.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : IRequest<Result>;
